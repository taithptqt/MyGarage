"""Selectable ALPR models and tiled detection for small plates."""
import re
import math
from statistics import mean
from run_demo import ALPR, cv2, ort
from runtime_config import execution_providers

PROVIDERS = execution_providers(ort.get_available_providers())


class Recognition:
    def __init__(self):
        self.models = {}

    def get_model(self, mode):
        key = mode if mode in ("fast", "balanced") else "accurate"
        if key not in self.models:
            options = ort.SessionOptions()
            options.intra_op_num_threads = 2
            options.inter_op_num_threads = 1
            options.add_session_config_entry("session.intra_op.allow_spinning", "0")
            options.add_session_config_entry("session.inter_op.allow_spinning", "0")
            self.models[key] = ALPR(
                detector_model={"fast":"yolo-v9-t-384-license-plate-end2end", "balanced":"yolo-v9-t-640-license-plate-end2end", "accurate":"yolo-v9-s-608-license-plate-end2end"}[key],
                ocr_model="cct-xs-v2-global-model" if key == "fast" else "cct-s-v2-global-model",
                detector_providers=PROVIDERS, ocr_providers=PROVIDERS,
                detector_sess_options=options, ocr_sess_options=options,
            )
        return self.models[key]

    def detect(self, frame, mode="accurate"):
        model = self.get_model(mode)
        height, width = frame.shape[:2]
        regions = [(0, 0, width, height)]
        if mode == "deep":
            # Overlapping crops keep distant plates larger at the detector input.
            tw, th = int(width * .60), int(height * .60)
            regions += [(x, y, x+tw, y+th) for y in (0, height-th) for x in (0, width-tw)]
        elif mode in ("balanced", "accurate") and height > width * 1.25:
            # A tall frame shrinks small plates excessively at the square model input.
            # Cover it with overlapping near-square views, bounded to four passes.
            edge = min(height, max(width, math.ceil(height / 3.4)))
            count = min(4, max(2, math.ceil((height-edge) / (edge*.8)) + 1))
            starts = [round(i*(height-edge)/(count-1)) for i in range(count)]
            regions = [(0, y, width, y+edge) for y in starts]
        candidates = []
        for x, y, right, bottom in regions:
            for detection in model.detector.predict(frame[y:bottom, x:right]):
                b = detection.bounding_box
                candidates.append(([max(0, x+b.x1), max(0, y+b.y1), min(width, x+b.x2), min(height, y+b.y2)], float(detection.confidence)))
        boxes = [[b[0],b[1],b[2]-b[0],b[3]-b[1]] for b, _ in candidates]
        keep = cv2.dnn.NMSBoxes(boxes, [s for _,s in candidates], .4, .45) if boxes else []
        return [candidates[int(index)] for index in keep]

    def read(self, frame, box, mode="accurate"):
        model = self.get_model(mode)
        height, width = frame.shape[:2]
        x1,y1,x2,y2 = box
        px,py = max(2,round((x2-x1)*.03)),max(2,round((y2-y1)*.03))
        crop = frame[max(0,y1-py):min(height,y2+py),max(0,x1-px):min(width,x2+px)]
        if crop.size == 0:
            return "",0.
        ocr = model.ocr.predict(crop)
        text = ocr.text if ocr else ""
        confidence = mean(ocr.confidence) if ocr and isinstance(ocr.confidence,list) and ocr.confidence else float(ocr.confidence or 0) if ocr else 0
        return text,confidence

    def infer(self, frame, mode="accurate"):
        details = []
        for box, score in self.detect(frame, mode):
            text,confidence = self.read(frame, box, mode)
            incomplete = len(re.sub(r"[^A-Z0-9]", "", text.upper())) < 7
            details.append({"text":text,"box":box,"detection_confidence":score,"ocr_confidence":confidence,"possibly_incomplete":incomplete,"uncertain":incomplete or confidence<.8})
        return details

    def predict(self, frame, mode="accurate"):
        details = self.infer(frame, mode)
        height,width = frame.shape[:2]
        annotated = frame.copy()
        for row in details:
            x1,y1,x2,y2 = row['box']
            color = (0,180,255) if row['uncertain'] else (0,255,0)
            cv2.rectangle(annotated,(x1,y1),(x2,y2),color,max(2,width//1000))
            label = (row['text'] or '?') + (' [?]' if row['uncertain'] else '')
            cv2.putText(annotated,label,(x1,max(18,y1-8)),cv2.FONT_HERSHEY_SIMPLEX,max(.55,width/2300),color,max(1,width//1200))
        return annotated,details
