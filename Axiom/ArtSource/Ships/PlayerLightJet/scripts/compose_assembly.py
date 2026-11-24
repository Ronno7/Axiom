"""Create labeled review sheets and normal/slow GIFs from the original 3D renders."""
import json
import sys
from pathlib import Path
from PIL import Image,ImageDraw,ImageFont

OUT=Path(__file__).resolve().parents[1] / 'reviews'/sys.argv[1]
BG='#0C141F';TEXT='#E6FFFF';MUTED='#8198A6'


def font(size):return ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf',size)


def sheet(name,title,subtitle,entries):
    w,h=600,400
    result=Image.new('RGB',(1200,110+((len(entries)+1)//2)*440+35),BG)
    d=ImageDraw.Draw(result);d.text((24,15),title,font=font(30),fill=TEXT)
    d.text((24,60),subtitle,font=font(18),fill=MUTED)
    for i,(file,label) in enumerate(entries):
        im=Image.open(OUT/(file+'.png')).convert('RGB');im.thumbnail((w,h),Image.Resampling.LANCZOS)
        x,y=i%2*w,110+i//2*440;result.paste(im,(x+(w-im.width)//2,y+(h-im.height)//2))
        d.text((x+20,y+h+5),label,font=font(18),fill=TEXT)
    d.text((24,result.height-28),'AXIOM / '+OUT.name+' / Blender study; Unity integration pending',font=font(15),fill=MUTED)
    result.save(OUT/name)


sheet('interior-sheet.png','SWEPT DART / INTERNAL ASSEMBLIES',
      'Faceted drives / reclined cockpit / wing supports / approved exterior retained',
      [('interior','01 / Exterior hidden: front quarter'),('interior-rear','02 / Exterior hidden: rear quarter'),
       ('cockpit','03 / Seat, controls and console'),('interior-bottom','04 / Keel and underside supports')])
sheet('assembly-sheet.png','SWEPT DART / INSIDE-OUT CONSTRUCTION',
      'Overlapping local reveals; internals remain visible before the upper shell closes.',
      [('assembly-stage-10','01 / Frame and drives arriving'),('assembly-stage-18','02 / Cockpit and wing supports'),
       ('assembly-stage-22','03 / Interior exposed inside construction traces'),('assembly-stage-29','04 / Lower skin consolidates'),
       ('assembly-stage-37','05 / Upper shell closes'),('assembly-stage-40','06 / Original exterior; internals stop drawing')])


def animation(prefix,name,slow=False):
    frames=[]
    for frame in range(1,41):
        image=Image.open(OUT/'frames'/('%s-%03d.png'%(prefix,frame))).convert('RGB')
        canvas=Image.new('RGB',(720,530),BG);canvas.paste(image,(0,0))
        d=ImageDraw.Draw(canvas)
        label='4x SLOW REVIEW' if slow else 'NORMAL SPEED / 1.67 s build'
        d.text((16,484),label+' / '+OUT.name,font=font(17),fill=TEXT)
        d.text((16,507),'Blender preview / studio light / not a game capture',font=font(13),fill=MUTED)
        frames.append(canvas)
    atlas=Image.new('RGB',(720*8,530*5))
    for i,frame in enumerate(frames):atlas.paste(frame,(i%8*720,i//8*530))
    palette=atlas.quantize(colors=256)
    indexed=[f.quantize(palette=palette,dither=Image.Dither.NONE) for f in frames]
    # GIF time has 10 ms resolution. Distribute rounding; do not silently label 25 fps as 24.
    durations=[round((i+1)*100/24)*10-round(i*100/24)*10 for i in range(40)]
    if slow:durations=[d*4 for d in durations]
    durations[-1]+=900
    indexed[0].save(OUT/name,save_all=True,append_images=indexed[1:],duration=durations,loop=0,disposal=2)


animation('assembly','assembly-normal.gif')
animation('assembly','assembly-slow.gif',True)
animation('chase-assembly','chase-assembly.gif')
print('ASSEMBLY_MEDIA_COMPLETE',OUT)
