"""Download official model assets, verify hashes and validate the local engine."""
import hashlib
import json
import subprocess
import sys
import urllib.request
from pathlib import Path

root = Path(__file__).resolve().parents[2]
runtime = root / 'artifacts' / 'face-local'
runtime.mkdir(parents=True, exist_ok=True)
assets = [
    ('yunet.onnx', 'face_detection_yunet/face_detection_yunet_2023mar.onnx',
     '8f2383e4dd3cfbb4553ea8718107fc0423210dc964f9f4280604804ed2552fa4'),
    ('sface.onnx', 'face_recognition_sface/face_recognition_sface_2021dec.onnx',
     '0ba9fbfa01b5270c96627c4ef784da859931e02f04419c829e83484087c34e79'),
]
for name, remote, expected in assets:
    path = runtime / name
    if path.exists() and hashlib.sha256(path.read_bytes()).hexdigest() == expected:
        print('MODEL_OK: ' + name, flush=True)
    else:
        print('Downloading official OpenCV model: ' + name, flush=True)
        url = 'https://media.githubusercontent.com/media/opencv/opencv_zoo/main/models/' + remote
        temporary = path.with_suffix('.download')
        try:
            with urllib.request.urlopen(url, timeout=120) as response, temporary.open('wb') as output:
                while chunk := response.read(1024 * 1024):
                    output.write(chunk)
            if hashlib.sha256(temporary.read_bytes()).hexdigest() != expected:
                raise RuntimeError('Model hash mismatch: ' + name)
            temporary.replace(path)
        finally:
            temporary.unlink(missing_ok=True)
    # Keep each model's upstream license alongside the runtime assets.
    license_path = runtime / (name + '.LICENSE')
    try:
        if license_path.exists():
            continue
        license_url = 'https://raw.githubusercontent.com/opencv/opencv_zoo/main/models/' + remote.rsplit('/', 1)[0] + '/LICENSE'
        with urllib.request.urlopen(license_url, timeout=30) as response:
            license_path.write_bytes(response.read())
    except Exception:
        raise RuntimeError('Could not retrieve model license: ' + name)

check = subprocess.run([sys.executable, str(root / 'scripts' / 'face-local' / 'worker.py'), '--models', str(runtime), '--check'],
                       capture_output=True, text=True, timeout=60)
result = json.loads(check.stdout)
if check.returncode != 0 or not result.get('ready'):
    raise RuntimeError('Face engine self-check failed: ' + str(result.get('detail', 'unknown model/runtime error')))
(runtime / 'ready.json').write_text(json.dumps(result), encoding='utf-8')
print('LOCAL_FACE_READY: ' + result['opencv'], flush=True)
