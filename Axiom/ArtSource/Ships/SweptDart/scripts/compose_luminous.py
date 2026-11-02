"""Compose labeled review media from Blender renders; pass v13 to select that revision."""
import json
import sys
from pathlib import Path
from PIL import Image,ImageDraw,ImageFont

REVISION=sys.argv[1] if len(sys.argv)>1 else 'v12'
OUT=Path(__file__).resolve().parents[1] / 'reviews'/REVISION
BG='#0C141F';TEXT='#E6FFFF';MUTED='#8198A6'
def font(n):return ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf',n)

entries=[(6,'01 / Controls initiate'),(18,'02 / Primitive volumes spread'),(34,'03 / Mechanical contours refine'),
         (50,'04 / Luminous framework complete'),(59,'05 / Matter grows from region edges'),
         (65,'06 / White skin consolidates'),(72,'07 / Completed exterior; temporary geometry off')]
if REVISION=='v13':
    entries=[(6,'01 / Controls initiate'),(28,'02 / Parts begin resolving locally'),
             (40,'03 / Matter starts while contours develop'),(46,'04 / Edges inward; center outward'),
             (54,'05 / Multiple fronts meet'),(62,'06 / Remaining regions fill'),(72,'07 / Completed exterior')]
sheet=Image.new('RGB',(1200,110+((len(entries)+1)//2)*440+30),BG);d=ImageDraw.Draw(sheet)
d.text((24,18),'SWEPT DART / LUMINOUS CONSTRUCTION',font=font(30),fill=TEXT)
d.text((24,65),REVISION+' / 3 seconds / '+('overlapping build / edges inward + center outward' if REVISION=='v13' else 'controls outward / coarse to fine / late material growth'),font=font(19),fill=MUTED)
for i,(frame,label) in enumerate(entries):
    im=Image.open(OUT/('stage-%03d.png'%frame)).convert('RGB').resize((600,400),Image.Resampling.LANCZOS)
    x,y=(300 if i==len(entries)-1 and len(entries)%2 else i%2*600),110+i//2*440
    sheet.paste(im,(x,y));d.text((x+18,y+407),label,font=font(18),fill=TEXT)
d.text((24,sheet.height-30),'Blender preview / approved exterior retained / Unity integration pending',font=font(16),fill=MUTED)
sheet.save(OUT/'assembly-sheet.png')

report={}
for prefix in ('assembly','chase'):
    frames=[]
    for f in range(1,73):
        im=Image.open(OUT/'frames'/('%s-%03d.png'%(prefix,f))).convert('RGB')
        frames.append(im)
    for slow in ([False,True] if prefix=='assembly' else [False]):
        labeled=[]
        for im in frames:
            canvas=Image.new('RGB',(720,530),BG);canvas.paste(im,(0,0));d=ImageDraw.Draw(canvas)
            d.text((16,484),('4x SLOW INSPECTION' if slow else 'NORMAL SPEED / 3 s build')+' / '+REVISION,font=font(17),fill=TEXT)
            d.text((16,507),'Blender lighting and bloom / not a game capture',font=font(13),fill=MUTED);labeled.append(canvas)
        atlas=Image.new('RGB',(720*9,530*8))
        for i,im in enumerate(labeled):atlas.paste(im,(i%9*720,i//9*530))
        palette=atlas.quantize(colors=256)
        indexed=[im.quantize(palette=palette,dither=Image.Dither.NONE) for im in labeled]
        durations=[round((i+1)*100/24)*10-round(i*100/24)*10 for i in range(72)]
        if slow:durations=[x*4 for x in durations]
        durations[-1]+=900
        name=prefix+('-slow.gif' if slow else '-normal.gif')
        indexed[0].save(OUT/name,save_all=True,append_images=indexed[1:],duration=durations,loop=0,disposal=2)
        check=Image.open(OUT/name);total=0
        for i in range(check.n_frames):check.seek(i);total+=check.info.get('duration',0)
        report[name]={'encoded_frames':check.n_frames,'duration_ms':total,'final_hold_ms':900}
(OUT/'media-checks.json').write_text(json.dumps(report,indent=2)+'\n')
print(json.dumps(report))
