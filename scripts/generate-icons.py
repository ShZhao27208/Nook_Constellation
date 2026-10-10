"""Reproducible vector-inspired Nook Constellation icons. Requires Pillow."""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFilter, ImageFont
import math
ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'assets/branding'; OUT.mkdir(parents=True,exist_ok=True)
S=1024
im=Image.new('RGBA',(S,S)); px=im.load()
for y in range(S):
 for x in range(S):
  t=(x+y)/(2*S)
  halo=math.exp(-(((x-710)/400)**2+((y-250)/430)**2))*0.5
  px[x,y]=(int(20-11*t+8*halo),int(39-24*t+29*halo),int(70-39*t+40*halo),255)
mask=Image.new('L',(S,S)); d=ImageDraw.Draw(mask);d.rounded_rectangle((68,68,956,956),radius=218,fill=255)
im.putalpha(mask)
# Warm light behind the leading star.
glow=Image.new('RGBA',(S,S)); g=ImageDraw.Draw(glow);g.ellipse((577,147,861,431),fill=(255,193,103,55));glow=glow.filter(ImageFilter.GaussianBlur(64));glow.putalpha(Image.composite(glow.getchannel('A'),Image.new('L',(S,S)),mask)); im=Image.alpha_composite(im,glow)
d=ImageDraw.Draw(im)
d.rounded_rectangle((70,70,954,954),radius=217,outline=(173,207,246,95),width=3)
d.arc((151,151,873,873),205,325,fill=(108,172,231,26),width=2)
# A sheltered corner: the nook that holds the constellation.
d.line([(222,534),(222,767),(454,767)],fill=(118,162,207,140),width=15,joint='curve')
d.ellipse((214,527,230,543),fill=(118,162,207,140));d.ellipse((446,759,462,775),fill=(118,162,207,140))
points=[(300,666),(418,452),(569,548),(726,289)]
d.line(points,fill=(153,197,231,230),width=13,joint='curve')
# Nodes have a fine rim and a warm central point.
for i,(x,y) in enumerate(points[:-1]):
 radius=25 if i!=1 else 19
 d.ellipse((x-radius-5,y-radius-5,x+radius+5,y+radius+5),fill=(49,83,119,255))
 d.ellipse((x-radius,y-radius,x+radius,y+radius),fill=(219,238,251,255) if i!=2 else (248,211,146,255))
# Four-point star, gold silhouette with a luminous centre.
x,y=points[-1]
star=[(x,y-108),(x+26,y-28),(x+94,y),(x+26,y+27),(x,y+109),(x-27,y+27),(x-94,y),(x-27,y-28)]
d.polygon(star,fill=(251,211,138,255))
d.polygon([(x,y-70),(x+14,y-14),(x+59,y),(x+14,y+14),(x,y+69),(x-14,y+14),(x-59,y),(x-14,y-14)],fill=(255,244,207,255))
for x,y,r in [(315,259,4),(805,627,4),(564,244,3)]: d.ellipse((x-r,y-r,x+r,y+r),fill=(140,182,220,125))
im.save(OUT/'nook-constellation-1024.png')
im.resize((256,256),Image.Resampling.LANCZOS).save(OUT/'nook-constellation.png')
im.save(ROOT/'src/Nook.Constellation/app.ico',sizes=[(16,16),(20,20),(24,24),(32,32),(40,40),(48,48),(64,64),(128,128),(256,256)])
# Small optical variant: omit secondary decoration and increase stroke weight.
tray=Image.new('RGBA',(256,256));td=ImageDraw.Draw(tray)
td.rounded_rectangle((8,8,248,248),radius=58,fill=(17,31,53,255),outline=(146,181,221,255),width=5)
td.line([(57,178),(102,109),(144,140),(187,70)],fill=(209,233,252,255),width=10)
for x,y in [(57,178),(102,109),(144,140)]:td.ellipse((x-10,y-10,x+10,y+10),fill=(220,237,251,255))
x,y=187,70;td.polygon([(x,y-34),(x+10,y-10),(x+31,y),(x+10,y+10),(x,y+35),(x-10,y+10),(x-31,y),(x-10,y-10)],fill=(255,222,155,255))
tray.save(ROOT/'src/Nook.Constellation/tray.ico',sizes=[(16,16),(20,20),(24,24),(32,32),(48,48)])
tray.resize((64,64),Image.Resampling.LANCZOS).save(OUT/'tray-preview.png')
# Standalone source SVG retains an editable vector master.
svg='''<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 1024 1024">
<defs><linearGradient id="night" x2="1" y2="1"><stop stop-color="#142746"/><stop offset="1" stop-color="#090f20"/></linearGradient><radialGradient id="light"><stop stop-color="#ffe3a6" stop-opacity=".22"/><stop offset="1" stop-color="#ffe3a6" stop-opacity="0"/></radialGradient></defs>
<rect x="68" y="68" width="888" height="888" rx="218" fill="url(#night)" stroke="#adcff6" stroke-opacity=".37" stroke-width="3"/>
<circle cx="726" cy="289" r="190" fill="url(#light)"/>
<path d="M222 534V767H454" fill="none" stroke="#76a2cf" stroke-opacity=".55" stroke-width="15" stroke-linecap="round" stroke-linejoin="round"/>
<path d="M300 666L418 452L569 548L726 289" fill="none" stroke="#99c5e7" stroke-width="13" stroke-linecap="round" stroke-linejoin="round"/>
<g stroke="#315377" stroke-width="10"><circle cx="300" cy="666" r="25" fill="#dbeefb"/><circle cx="418" cy="452" r="19" fill="#dbeefb"/><circle cx="569" cy="548" r="25" fill="#f8d392"/></g>
<path d="M726 181L752 261L820 289L752 316L726 398L699 316L632 289L699 261Z" fill="#fbd38a"/>
<path d="M726 219L740 275L785 289L740 303L726 358L712 303L667 289L712 275Z" fill="#fff4cf"/>
<g fill="#8cb6dc" opacity=".5"><circle cx="315" cy="259" r="4"/><circle cx="805" cy="627" r="4"/><circle cx="564" cy="244" r="3"/></g></svg>'''
(OUT/'nook-constellation.svg').write_text(svg)
# Preview across desktop surfaces and at native Windows sizes.
sheet=Image.new('RGB',(1120,620),(13,20,35));sd=ImageDraw.Draw(sheet)
sd.rounded_rectangle((28,28,1092,592),radius=26,fill=(22,32,51))
sheet.paste(im.resize((440,440),Image.Resampling.LANCZOS),(55,62),im.resize((440,440),Image.Resampling.LANCZOS))
sd.rounded_rectangle((550,76,1048,290),radius=20,fill=(238,241,247))
sd.rounded_rectangle((550,314,1048,528),radius=20,fill=(8,14,26))
for yy in [106,344]:
 for xx,size in [(570,160),(766,80),(892,48),(978,24)]:
  icon=(tray if size<=48 else im).resize((size,size),Image.Resampling.LANCZOS);sheet.paste(icon,(xx,yy+(160-size)//2),icon)
try: font=ImageFont.truetype('/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf',22)
except OSError: font=ImageFont.load_default()
sd.text((82,522),'NOOK CONSTELLATION',font=font,fill=(222,230,243))
sheet.save(OUT/'icon-preview.png')
