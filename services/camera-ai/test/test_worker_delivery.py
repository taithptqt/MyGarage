from urllib.error import URLError

import pytest

from event_spool import EventSpool
import worker


def test_network_failure_retains_event_and_retry_uses_same_identity(tmp_path, monkeypatch):
    worker.stop.clear()
    spool=EventSpool(tmp_path/"queue.sqlite3")
    original={"eventKey":"persistent-event", "plate":"65H02902"}
    spool.observe(1,"65H02902",original,now=100)
    calls=[]

    def offline(*args):
        calls.append(args[-1])
        raise URLError("offline")

    monkeypatch.setattr(worker,"api",offline)
    with pytest.raises(URLError):
        worker.deliver("http://localhost",1,"test-key",spool)
    assert spool.first(1)==("persistent-event",original)
    spool.close()
    # Simulate process restart while the queue is unacknowledged.
    spool=EventSpool(tmp_path/"queue.sqlite3")
    monkeypatch.setattr(worker,"api",lambda *args:calls.append(args[-1]))
    worker.deliver("http://localhost",1,"test-key",spool)
    assert calls==[original,original]
    assert spool.first(1) is None
    spool.close()
