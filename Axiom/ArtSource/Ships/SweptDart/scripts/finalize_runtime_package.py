"""Compose review sheets, validate staged links and checksum the handoff bundle."""
import hashlib
import json
import re
import subprocess
import zipfile
from pathlib import Path
from urllib.parse import unquote
from PIL import Image,ImageDraw,ImageFont
import imageio_ffmpeg

SHIP=Path(__file__).resolve().parents[1]
ROOT=SHIP/'reviews/v14';PACKAGE=ROOT/'export';QA=ROOT/'qa'
font=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',22)
for name,files in [('sequence-sheet',[(f'stage-{f:03}.png',f'{(f-1)/24:.2f}s') for f in (6,28,40,46,50,54,62,72)]),
                   ('shield-sheet',[(f'shield-{n}.png',f'{n}/3 shields') for n in (3,2,1,0)])]:
    board=Image.new('RGB',(1200,((len(files)+1)//2)*430),'#061218')
    for i,(file,label) in enumerate(files):
        im=Image.open(QA/file);im.thumbnail((600,400));x=i%2*600;y=i//2*430
        board.paste(im,(x,y));ImageDraw.Draw(board).text((x+18,y+402),label,font=font,fill='#E6FFFF')
    board.save(QA/(name+'.jpg'),quality=93)
frames=(6,28,40,46,54,62,72)
board=Image.new('RGB',(960,340*len(frames)),'#061218')
for i,f in enumerate(frames):
    for j,path in enumerate((SHIP/'reviews/v13'/f'stage-{f:03}.png',QA/f'stage-{f:03}.png')):
        im=Image.open(path);im.thumbnail((480,320));board.paste(im,(j*480,i*340))
        ImageDraw.Draw(board).text((j*480+8,i*340+320),f'{"v13 source" if j==0 else "v14 export"} frame {f}',fill='white')
board.save(QA/'comparison.jpg',quality=93)
ff=imageio_ffmpeg.get_ffmpeg_exe()
frames=sorted((QA/'frames').glob('frame-*.png'));assert len(frames)==72
for path in frames:
    with Image.open(path) as im:im.verify()
subprocess.run([ff,'-v','error','-y','-framerate','24','-i',str(QA/'frames/frame-%03d.png'),
    '-frames:v','72','-c:v','libx264','-crf','18','-pix_fmt','yuv420p','-movflags','+faststart',str(QA/'runtime-sequence.mp4')],check=True)
subprocess.run([ff,'-v','error','-xerror','-i',str(QA/'runtime-sequence.mp4'),'-f','null','-'],check=True)
reader=imageio_ffmpeg.read_frames(str(QA/'runtime-sequence.mp4'));meta=next(reader)
count=sum(1 for _ in reader);reader.close()
assert count==72 and abs(meta['duration']-3)<.01 and meta['fps']==24
for path in [PACKAGE/'README.md',SHIP/'README.md',SHIP/'docs/TECHNICAL_NOTES.md']:
    for target in re.findall(r'\]\(([^)]+)\)',path.read_text(encoding='utf8')):
        if target.startswith(('https:','http:','#')):continue
        assert (path.parent/unquote(target.split('#')[0])).exists(),(path,target)
for name in ('validation.json','data-validation.json'):
    assert json.loads((PACKAGE/name).read_text(encoding='utf8'))['passed']
qa_record=dict(animation_frames=72,seconds=3,video_fully_decoded=True,links_checked=True,
    images_verified=84,visual_review='Eight construction stages and four shield states inspected in Blender renders.',
    limitation='No Unity import, shader compilation, engine playback or performance measurement.')
(QA/'review-validation.json').write_text(json.dumps(qa_record,indent=2)+'\n',encoding='utf8')
hashes={str(p.relative_to(PACKAGE)).replace('\\','/'):hashlib.sha256(p.read_bytes()).hexdigest()
    for p in PACKAGE.rglob('*') if p.is_file() and p.suffix!='.blend1' and p.name!='checksums.json'}
(PACKAGE/'checksums.json').write_text(json.dumps(hashes,indent=2)+'\n',encoding='utf8')
bundle=ROOT/'SweptDart-v14-import.zip'
with zipfile.ZipFile(bundle,'w',zipfile.ZIP_DEFLATED,compresslevel=6) as z:
    for p in sorted(PACKAGE.rglob('*')):
        if p.is_file() and p.suffix!='.blend1':z.write(p,'export/'+p.relative_to(PACKAGE).as_posix())
    for name in ('sequence-sheet.jpg','shield-sheet.jpg','comparison.jpg','runtime-sequence.mp4','runtime-review.blend','review-validation.json'):
        z.write(QA/name,'qa/'+name)
with zipfile.ZipFile(bundle) as z:
    assert z.testzip() is None
    for name,digest in hashes.items():assert hashlib.sha256(z.read('export/'+name)).hexdigest()==digest
print('HANDOFF_BUNDLE_VERIFIED',len(hashes),'files,',bundle.stat().st_size,'bytes')
