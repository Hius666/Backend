"""Local YuNet + SFace feature extraction. No photos are written to disk."""
import argparse
import base64
import json
import os
import sys
from pathlib import Path


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--models', required=True)
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    try:
        os.environ.setdefault('OPENCV_IO_MAX_IMAGE_PIXELS', '24000000')
        import cv2
        import numpy as np
        cv2.setNumThreads(1)
        models = Path(args.models)
        detector = cv2.FaceDetectorYN.create(str(models / 'yunet.onnx'), '', (320, 320), 0.9, 0.3, 5000)
        recognizer = cv2.FaceRecognizerSF.create(str(models / 'sface.onnx'), '')
        if args.check:
            _, faces = detector.detect(np.zeros((320, 320, 3), dtype=np.uint8))
            if faces is not None and len(faces):
                raise RuntimeError('Unexpected face on blank input')
            # Force a recognition forward pass too, not just ONNX file parsing.
            feature = recognizer.feature(np.zeros((112, 112, 3), dtype=np.uint8))
            if feature.size != 128 or not np.all(np.isfinite(feature)):
                raise RuntimeError('Invalid recognition output')
            print(json.dumps({'ready': True, 'opencv': cv2.__version__}))
            return
        request = json.loads(sys.stdin.buffer.read(6_000_001))
        data = base64.b64decode(request['image'], validate=True)
        if not 100 <= len(data) <= 4_000_000:
            raise ValueError('image_size')
        image = cv2.imdecode(np.frombuffer(data, dtype=np.uint8), cv2.IMREAD_COLOR)
        if image is None:
            raise ValueError('invalid_image')
        h, w = image.shape[:2]
        if max(h, w) > 1600:
            image = cv2.resize(image, (max(1, round(w * 1600 / max(h, w))), max(1, round(h * 1600 / max(h, w)))))
        detector.setInputSize((image.shape[1], image.shape[0]))
        _, faces = detector.detect(image)
        count = 0 if faces is None else len(faces)
        if count != 1:
            print(json.dumps({'error': 'face_count', 'count': count}))
            return
        if min(faces[0][2], faces[0][3]) < 70:
            print(json.dumps({'error': 'face_too_small'}))
            return
        aligned = recognizer.alignCrop(image, faces[0])
        feature = recognizer.feature(aligned).flatten().astype(float)
        norm = float(np.linalg.norm(feature))
        if feature.size != 128 or not np.isfinite(norm) or norm < 1e-8:
            raise ValueError('invalid_feature')
        print(json.dumps({'feature': (feature / norm).tolist()}, allow_nan=False))
    except Exception as exc:
        # Never print the request, photo, template or embedding in diagnostic errors.
        result = {'error': str(exc) if isinstance(exc, ValueError) and str(exc) in ('invalid_image', 'image_size', 'invalid_feature') else 'engine_error'}
        if args.check:
            result['detail'] = type(exc).__name__ + ': ' + str(exc)
        print(json.dumps(result))
        sys.exit(1)


if __name__ == '__main__':
    main()
