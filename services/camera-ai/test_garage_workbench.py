"""Opt-in real AI HTTP test with an isolated Garage receiver; no development DB writes."""
import hashlib
import json
import os
import socket
import subprocess
import sys
import tempfile
import threading
import time
import unittest
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from urllib.error import HTTPError, URLError
from urllib.request import Request, urlopen


@unittest.skipUnless(os.environ.get('CAMERA_BRIDGE_E2E') == '1', 'Set CAMERA_BRIDGE_E2E=1 for real local inference')
class WorkbenchBridgeE2E(unittest.TestCase):
    def test_original_workbench_sends_entry_exit_and_retries_offline(self):
        attempts, events = [], []
        state = {'offline': False}
        credential = 'c'*64

        class Receiver(BaseHTTPRequestHandler):
            def log_message(self, *_):
                pass

            def do_GET(self):
                self.assert_key()
                direction = 'ENTRY' if '/1/' in self.path else 'EXIT'
                self.reply(200, {'direction': direction})

            def assert_key(self):
                if self.headers.get('X-Camera-Key') != credential:
                    raise AssertionError('Missing device authentication')

            def reply(self, status, payload):
                body = json.dumps(payload).encode()
                self.send_response(status)
                self.send_header('Content-Length', str(len(body)))
                self.end_headers()
                self.wfile.write(body)

            def do_POST(self):
                self.assert_key()
                payload = json.loads(self.rfile.read(int(self.headers['Content-Length'])))
                if self.path.endswith('/observations'):
                    attempts.append((self.path, payload))
                    if state['offline']:
                        self.reply(503, {})
                        return
                    events.append((self.path, payload))
                self.reply(200, {'id': len(events)})

        receiver = ThreadingHTTPServer(('127.0.0.1', 0), Receiver)
        threading.Thread(target=receiver.serve_forever, daemon=True).start()
        with socket.socket() as free_port:
            free_port.bind(('127.0.0.1', 0))
            port = free_port.getsockname()[1]
        root = Path(__file__).resolve().parent
        with tempfile.TemporaryDirectory() as directory:
            env = {**os.environ, 'CAMERA_AI_BRIDGE_DIRECTORY': directory, 'CAMERA_AI_BACKEND': 'cpu'}
            with open(Path(directory)/'server.log', 'w') as output:
                process = subprocess.Popen([sys.executable, 'web_app.py', '--port', str(port)], cwd=root, env=env,
                                           stdout=output, stderr=output, creationflags=getattr(subprocess, 'CREATE_NO_WINDOW', 0))
                def call(path, data=None, origin='http://localhost:5173'):
                    request = Request(f'http://127.0.0.1:{port}{path}',
                                      data=json.dumps(data).encode() if data is not None else None,
                                      headers={'Content-Type': 'application/json', 'Origin': origin})
                    with urlopen(request, timeout=45) as response:
                        return json.loads(response.read())

                def wait_for(predicate, seconds=15):
                    deadline = time.monotonic()+seconds
                    while time.monotonic() < deadline:
                        try:
                            if predicate():
                                return
                        except (URLError, ConnectionError):
                            pass
                        time.sleep(.2)
                    self.fail('Timed out waiting for AI/queued delivery')

                def pair(camera):
                    return call('/garage/connect', {'garageUrl': f'http://127.0.0.1:{receiver.server_port}',
                                                    'cameraId': camera, 'credential': credential})
                try:
                    wait_for(lambda: call('/health')['status'] == 'ok')
                    for file in ['web.html', 'web.js', 'tracking.js']:
                        with urlopen(f'http://127.0.0.1:{port}/' + ('' if file == 'web.html' else file)) as response:
                            self.assertEqual(hashlib.sha256((root/file).read_bytes()).digest(), hashlib.sha256(response.read()).digest())
                    with self.assertRaises(HTTPError) as denied:
                        call('/garage/connect', {}, origin='http://untrusted.example')
                    self.assertEqual(403, denied.exception.code)
                    self.assertEqual(1, pair(1)['activeCameraId'])
                    source = call('/video/sample', {})['source']
                    for stamp in [1, 1.08]:
                        result = call('/video/analyze', {'source': source, 'time': stamp, 'mode': 'balanced', 'max_width': 1280})
                        self.assertIn('65H02902', result['plates'])
                    wait_for(lambda: any('/1/observations' in path for path, _ in events))
                    entry = [payload for path, payload in events if '/1/' in path and payload['plate'] == '65H02902']
                    self.assertEqual(1, len(entry))
                    self.assertTrue(entry[0]['snapshotBase64'])
                    self.assertTrue(entry[0]['cropBase64'])
                    call('/video/close', {'source': source})
                    pair(2)
                    state['offline'] = True
                    source = call('/video/sample', {})['source']
                    call('/video/analyze', {'source': source, 'time': 1, 'mode': 'balanced', 'max_width': 1280})
                    wait_for(lambda: any('/2/observations' in path for path, _ in attempts))
                    self.assertGreaterEqual(call('/garage/status')['pending'], 1)
                    state['offline'] = False
                    wait_for(lambda: any('/2/observations' in path for path, _ in events))
                    exit_ids = [payload['eventKey'] for path, payload in attempts if '/2/' in path and payload['plate'] == '65H02902']
                    self.assertGreaterEqual(len(exit_ids), 2)
                    self.assertEqual(1, len(set(exit_ids)))
                    self.assertNotIn(credential, json.dumps(call('/garage/status')))
                finally:
                    process.terminate()
                    process.wait(timeout=15)
                    receiver.shutdown()
                    receiver.server_close()


if __name__ == '__main__':
    unittest.main()
