import tempfile
import unittest
from unittest.mock import patch
import numpy as np
from garage_bridge import GarageBridge


class GarageBridgeTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.calls, self.offline = [], False
        def transport(target, route, payload=None):
            self.calls.append((target['cameraId'], route, payload))
            if route == 'configuration':
                return {'direction': 'ENTRY' if target['cameraId'] == 1 else 'EXIT'}
            if self.offline:
                raise ConnectionError('Garage unavailable')
            return {'id': 99}
        self.transport = transport
        self.bridge = GarageBridge(self.temp.name, transport)
        self.frame = np.zeros((100, 200, 3), dtype=np.uint8)
        self.rows = [{'text': '30A-123.45', 'box': [10, 20, 80, 50], 'detection_confidence': .9, 'ocr_confidence': .95}]

    def tearDown(self):
        self.bridge.close()
        self.temp.cleanup()

    def pair(self, camera=1):
        return self.bridge.connect({'garageUrl': 'http://localhost:5062', 'cameraId': camera, 'credential': 'a'*64})

    def test_capture_never_calls_garage_and_deduplicates_stationary_car(self):
        self.pair()
        self.calls.clear()
        for _ in range(4):
            self.bridge.observe(self.frame, self.rows)
        self.assertEqual([], self.calls)
        self.assertEqual(1, self.bridge.status()['pending'])
        self.bridge.flush_once()
        events = [item for item in self.calls if item[1] == 'observations']
        self.assertEqual(1, len(events))
        self.assertEqual('30A12345', events[0][2]['plate'])
        self.assertIsNotNone(events[0][2]['snapshotBase64'])
        self.assertIsNotNone(events[0][2]['cropBase64'])
        self.assertEqual(0, self.bridge.status()['pending'])

    def test_offline_restart_and_retry_preserve_event_key(self):
        self.pair()
        self.bridge.observe(self.frame, self.rows)
        self.offline = True
        self.bridge.flush_once()
        first = next(item[2]['eventKey'] for item in self.calls if item[1] == 'observations')
        self.assertEqual(1, self.bridge.status()['pending'])
        self.bridge = GarageBridge(self.temp.name, self.transport)
        self.offline = False
        self.bridge.flush_once()
        delivered = [item[2]['eventKey'] for item in self.calls if item[1] == 'observations']
        self.assertEqual([first, first], delivered)
        self.assertEqual(0, self.bridge.status()['pending'])
        self.assertNotIn('credential', str(self.bridge.status()))

    def test_video_camera_binding_survives_switch_to_exit_camera(self):
        self.pair(1)
        video_camera = self.bridge.active
        self.pair(2)
        self.bridge.observe(self.frame, self.rows, video_camera)
        self.bridge.observe(self.frame, self.rows)
        self.bridge.flush_once()
        self.assertEqual([1, 2], [item[0] for item in self.calls if item[1] == 'observations'])

    def test_new_visit_after_absence_emits_new_id(self):
        self.pair()
        with patch('garage_bridge.time.time', return_value=100):
            self.bridge.observe(self.frame, self.rows)
        with patch('garage_bridge.time.time', return_value=120):
            self.bridge.observe(self.frame, self.rows)
        self.assertEqual(2, self.bridge.status()['pending'])

    def test_invalid_pairing_and_unconfigured_preview(self):
        self.bridge.observe(self.frame, self.rows)
        self.assertEqual(0, self.bridge.status()['pending'])
        for url in ['http://example.com', 'http://localhost:5062/path', 'http://user:password@localhost:5062']:
            with self.assertRaises(ValueError):
                self.bridge.connect({'garageUrl': url, 'cameraId': 1, 'credential': 'a'*64})


if __name__ == '__main__':
    unittest.main()
