"""Durable delivery and presence deduplication; acknowledge only after HTTP success."""
import json
import sqlite3
import time
from pathlib import Path


class EventSpool:
    def __init__(self, path):
        Path(path).parent.mkdir(parents=True, exist_ok=True)
        self.db = sqlite3.connect(path, timeout=30)
        self.db.execute("PRAGMA journal_mode=WAL")
        self.db.execute("PRAGMA synchronous=FULL")
        self.db.execute("CREATE TABLE IF NOT EXISTS pending (id TEXT PRIMARY KEY, camera INTEGER, payload TEXT)")
        self.db.execute("CREATE TABLE IF NOT EXISTS presence (camera INTEGER, plate TEXT, seen REAL, emitted INTEGER, PRIMARY KEY(camera,plate))")
        self.db.commit()

    def observe(self, camera, plate, payload, now=None, rearm_seconds=15):
        now = time.time() if now is None else now
        with self.db:
            old = self.db.execute("SELECT seen,emitted FROM presence WHERE camera=? AND plate=?", (camera, plate)).fetchone()
            emitted = bool(old and old[1] and now - old[0] < rearm_seconds)
            if not emitted:
                # A full disk raises and rolls back BOTH presence and queued event.
                self.db.execute("INSERT OR IGNORE INTO pending VALUES (?,?,?)", (payload["eventKey"], camera, json.dumps(payload)))
            self.db.execute("INSERT OR REPLACE INTO presence VALUES (?,?,?,1)", (camera, plate, now))
        return not emitted

    def touch(self, camera, plate, now=None):
        with self.db:
            self.db.execute("UPDATE presence SET seen=? WHERE camera=? AND plate=?", (time.time() if now is None else now, camera, plate))

    def first(self, camera):
        row = self.db.execute("SELECT id,payload FROM pending WHERE camera=? ORDER BY rowid LIMIT 1", (camera,)).fetchone()
        return (row[0], json.loads(row[1])) if row else None

    def acknowledge(self, event_id):
        with self.db:
            self.db.execute("DELETE FROM pending WHERE id=?", (event_id,))

    def close(self):
        self.db.close()
