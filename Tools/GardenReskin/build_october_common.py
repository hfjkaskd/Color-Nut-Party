"""Crop/resize built-in ImageGen blank sprites; never paint screenshot content."""
from october_common import *
SRC=WORK/'sources'
report=[]
def make(name,source,size,border=(0,0,0,0),sb=None,db=None,crop=None):
    report.append(import_sprite(name,SRC/(source+'.png'),size,border,sb,db,crop))

make('panel','panel',(1000,1100),(100,100,100,100))
make('primary-button','primary-button',(764,192),(56,56,56,56))
make('ribbon','ribbon',(975,186))
make('wood-ribbon','ribbon',(975,186))
make('input','input',(600,140),(36,36,36,36))
make('cream-card','input',(700,300),(42,42,42,42),(95,95,95,95),(42,42,42,42))
make('amount-normal','input',(365,221),(45,45,45,45),(100,100,100,100),(45,45,45,45))
make('amount-selected','amount-selected',(365,221),(45,45,45,45))
make('title','title',(700,130),(40,40,40,40))
make('wood-card','wood-card',(750,1200),(70,70,70,70))
make('cream-ribbon','cream-ribbon',(740,74))
make('progress-track','progress-track',(850,65),(32,0,32,0))
make('progress-fill','progress-fill',(850,65),(32,0,32,0))
make('close','close',(128,128))
make('background','background',(1080,1920))
# Blank crops for existing borderless backplates and full-width reward strips.
p=trim(Image.open(SRC/'panel.png').convert('RGBA'));pw,ph=p.size
p.crop((pw//4,ph//4,pw*3//4,ph*3//4)).resize((512,512),Image.Resampling.LANCZOS).save(OUT/'flat-cream.png');write_meta('flat-cream')
# Mechanical removal of rounded ends by central strip crop, keeping generated rails.
p.crop((pw//4,0,pw*3//4,ph)).resize((1080,280),Image.Resampling.LANCZOS).save(OUT/'reward-band.png');write_meta('reward-band',(0,12,0,12))
(WORK/'common-asset-build.json').write_text(json.dumps(report,indent=2),encoding='utf8')
print('Imported',len(report)+2,'blank source assets')
