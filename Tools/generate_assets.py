"""Original Tiny Workshop pixel art and synthesized audio. No external assets."""
from pathlib import Path
from PIL import Image, ImageDraw
import math, random, wave, struct

ROOT = Path(__file__).resolve().parents[1] / 'Assets/_TinyWorkshop'
ART = ROOT / 'Art'; AUDIO = ROOT / 'Audio'
ART.mkdir(parents=True, exist_ok=True); AUDIO.mkdir(parents=True, exist_ok=True)
random.seed(74)
P = dict(ink='#17252e', dark='#243640', wall='#30444b', rim='#526464', warm='#bca888', cream='#ede0ba', mint='#a2e0b2', green='#527e69', orange='#efaa54', shadow='#715443')

def save(name, size, painter):
    im=Image.new('RGBA',size,(0,0,0,0)); d=ImageDraw.Draw(im); painter(d); im.save(ART/(name+'.png'))

def background(d):
    # Calm, low-contrast backdrop: gameplay objects (robot, machine, gears, platforms, station)
    # must be the brightest, most detailed things on screen. No props that read as platforms.
    d.rectangle((0,0,639,359),fill='#141f26')
    for y in range(0,302):
        t=y/302
        c=(int(24+8*t),int(36+9*t),int(43+8*t))
        d.line((0,y,639,y),fill=c)
    # very faint, wide brick courses (barely above wall value)
    for y in range(70,300,36):
        d.line((0,y,639,y),fill='#24343b')
    # two soft structural pillars split the room into three zones without framing boxes
    for x in [212,424]:
        d.rectangle((x,30,x+9,301),fill='#1b2a31'); d.line((x+9,30,x+9,301),fill='#2b3c42')
    # one overhead pipe for the lamps to hang from
    d.rectangle((0,22,639,27),fill='#2c3b3f'); d.line((0,28,639,28),fill='#121c22')
    # single dim window in the middle zone: atmosphere, not detail
    d.rectangle((282,96,358,160),fill='#1a272d')
    d.rectangle((285,99,355,157),fill='#2a3d42')
    d.line((320,99,320,157),fill='#1a272d'); d.line((285,128,355,128),fill='#1a272d')
    # baseboard
    d.rectangle((0,294,639,301),fill='#1a262c')
    # floor: one clear walkable edge, quiet surface
    d.rectangle((0,302,639,359),fill='#3b3a33'); d.rectangle((0,302,639,304),fill='#9c8f70')
    d.rectangle((0,305,639,307),fill='#26282a')
    for y in range(320,360,16):
        d.line((0,y,639,y),fill='#36352f')

def robot(d):
    d.rectangle((7,3,8,7),fill=P['warm']);d.rectangle((6,1,9,3),fill=P['orange'])
    d.rectangle((3,7,20,23),fill=P['ink']); d.rectangle((4,6,19,22),fill=P['mint'])
    d.line((5,7,18,7),fill='#d8f2ca'); d.rectangle((5,10,18,17),fill='#31494a')
    d.rectangle((7,12,9,14),fill='#ffdfa1');d.rectangle((14,12,16,14),fill='#ffdfa1')
    d.rectangle((7,20,15,22),fill='#5b9e86');d.rectangle((10,20,12,21),fill='#f8d48c')
    d.rectangle((1,14,3,21),fill='#679b87');d.rectangle((20,14,22,21),fill='#679b87')
    d.rectangle((5,23,9,27),fill='#375950');d.rectangle((14,23,18,27),fill='#375950')
    d.rectangle((3,27,10,29),fill=P['mint']);d.rectangle((13,27,20,29),fill=P['mint'])

def machine(d):
    d.rectangle((9,82,86,93),fill=P['ink']);d.rectangle((13,81,82,88),fill='#7e8072')
    d.rectangle((14,22,74,82),fill=P['ink']);d.rectangle((17,20,71,81),fill='#777c6d')
    d.rectangle((18,21,69,25),fill='#b7b39a');d.rectangle((20,27,67,75),fill='#5e6c64')
    d.rectangle((27,4,59,20),fill=P['ink']);d.rectangle((30,5,56,18),fill='#8b9580')
    for x in range(32,56,6): d.rectangle((x,7,x+2,16),fill='#364849')
    d.rectangle((26,32,60,51),fill='#243a3d');d.rectangle((29,35,57,48),fill='#b6c38b')
    for x in range(32,56,6): d.rectangle((x,39,x+2,44),fill='#728265')
    d.rectangle((28,58,58,72),fill='#344b48');d.rectangle((32,61,54,69),fill='#b6a377')
    for x,y in [(21,29),(64,29),(21,74),(64,74)]:d.rectangle((x,y,x+2,y+2),fill='#c6c0a0')
    d.rectangle((73,39,88,57),fill=P['ink']);d.rectangle((72,42,91,53),fill='#a3987b');d.rectangle((83,45,94,51),fill='#394e4b')
    d.rectangle((3,49,15,69),fill='#283e40');d.rectangle((5,52,14,65),fill='#c49053')
    d.rectangle((4,73,14,80),fill='#bd975f')

def button(d):
    d.rectangle((1,2,19,16),fill='#513e30');d.rectangle((2,1,18,13),fill='#c6773e');d.rectangle((3,1,17,10),fill=P['orange']);d.line((4,2,16,2),fill='#ffe0a0');d.rectangle((9,4,11,7),fill='#ffe5ae')

def gear(d):
    for a in range(0,360,45):
        x=9+math.cos(math.radians(a))*6;y=9+math.sin(math.radians(a))*6
        d.rectangle((int(x)-2,int(y)-2,int(x)+2,int(y)+2),fill='#a46a36')
    d.ellipse((3,3,15,15),fill='#f0b85e',outline='#885b34');d.arc((4,4,14,14),180,300,fill='#ffe4a1',width=2);d.ellipse((7,7,11,11),fill='#534e3e')

def station(d):
    d.rectangle((4,31,74,60),fill=P['ink']);d.rectangle((7,34,71,56),fill='#6c8372');d.rectangle((8,34,70,36),fill='#b2bd94')
    d.rectangle((7,57,17,65),fill='#3b514b');d.rectangle((62,57,71,65),fill='#3b514b')
    d.rectangle((22,3,58,33),fill='#213b3e');d.rectangle((25,5,55,30),fill='#7d9274');d.rectangle((29,9,51,25),fill='#314b48')
    for x in [32,39,46]:d.rectangle((x,14,x+3,21),fill='#8ca77f')
    d.rectangle((14,41,64,51),fill='#304b45')
    for x in [18,28,38,48,58]: d.rectangle((x,43,x+3,48),fill='#b2b48a')
    d.rectangle((59,25,66,32),fill='#e4b56e');d.rectangle((8,28,19,33),fill='#a7b088')

def platform(d):
    d.rectangle((0,0,95,15),fill='#263c40');d.rectangle((0,0,95,3),fill='#cfb685');d.rectangle((1,4,94,10),fill='#84785e')
    for x in range(5,95,15):d.rectangle((x,6,x+3,8),fill='#beb08a')
    d.polygon([(7,11),(18,15),(25,11)],fill='#496058');d.polygon([(70,11),(78,15),(88,11)],fill='#496058')

save('workshop', (640,360), background);save('robot',(24,32),robot);save('machine',(96,96),machine)
save('button',(21,18),button);save('gear',(19,19),gear);save('station',(80,68),station);save('platform',(96,16),platform)
save('crate',(24,24),lambda d:(d.rectangle((0,0,23,23),fill='#514d3e'),d.rectangle((2,2,21,21),fill='#b68c59',outline='#e1b779',width=2),d.line((4,4,19,19),fill='#745d42',width=3),d.line((19,4,4,19),fill='#745d42',width=3)))
save('lamp',(24,32),lambda d:(d.rectangle((11,0,12,12),fill='#263c40'),d.polygon([(7,12),(16,12),(23,22),(0,22)],fill='#7d8f7b'),d.rectangle((5,23,18,25),fill='#f2c778'),d.line((5,13,17,13),fill='#b5b69a')))
save('spark',(3,3),lambda d:(d.line((1,0,1,2),fill='#fff0b7'),d.line((0,1,2,1),fill='#fff0b7')))
save('white',(2,2),lambda d:d.rectangle((0,0,1,1),fill='white'))
glow=Image.new('RGBA',(64,64),(0,0,0,0))
for gy in range(64):
    for gx in range(64):
        a=max(0,1-math.sqrt((gx-31.5)**2+(gy-31.5)**2)/32)**2
        glow.putpixel((gx,gy),(255,235,180,int(a*255)))
glow.save(ART/'glow.png')

def sound(name, duration, frequencies, volume=.35, noise=0, sweep=0):
    rate=22050; samples=[]; rng=random.Random(22)
    for i in range(int(duration*rate)):
        t=i/rate;env=min(1,t/.006)*max(0,1-t/duration)**2
        tone=sum(math.sin(2*math.pi*(f*t+sweep*t*t)) for f in frequencies)/len(frequencies)
        v=(tone*(1-noise)+(rng.random()*2-1)*noise)*env*volume
        samples.append(struct.pack('<h',int(max(-1,min(1,v))*32767)))
    with wave.open(str(AUDIO/(name+'.wav')),'wb') as w:w.setparams((1,2,rate,0,'NONE','not compressed'));w.writeframes(b''.join(samples))

sound('press',.10,[160,320],.36,.32,-190);sound('produce',.32,[440,660,880],.38,.12,320)
sound('collect',.22,[880,1320],.29,0,1800);sound('deliver',.38,[330,440,660],.34,.05,600)
sound('jump',.13,[200,400],.19,.08,1600);sound('land',.12,[90,180],.26,.55)
sound('step',.055,[130],.12,.75);sound('bounce',.13,[350,700],.22,.15,-500)
sound('denied',.16,[150,175],.16,.05);sound('start',.45,[330,495,660],.25,0,200)
rate=22050
with wave.open(str(AUDIO/'ambience.wav'),'wb') as w:
    w.setparams((1,2,rate,0,'NONE','not compressed'));buf=bytearray()
    for i in range(rate*8):
        t=i/rate;v=(math.sin(2*math.pi*55*t)+.3*math.sin(2*math.pi*110*t))*.1*(.8+.2*math.sin(2*math.pi*t/8))
        buf.extend(struct.pack('<h',int(v*32767)))
    w.writeframes(buf)
# original mellow arpeggio, 16 second seamless loop
rate=22050;duration=16;notes=[48,55,60,64,50,57,62,65,45,52,57,60,43,50,55,59]
with wave.open(str(AUDIO/'music.wav'),'wb') as w:
    w.setparams((1,2,rate,0,'NONE','not compressed'));buf=bytearray()
    for i in range(rate*duration):
        t=i/rate;n=int(t/.5)%32;local=t%.5;midi=notes[(n//2)%16]+(12 if n%2 else 0);f=440*2**((midi-69)/12)
        env=min(1,local/.015)*math.exp(-local*6);v=(math.sin(2*math.pi*f*local)+.25*math.sin(2*math.pi*f*2*local))*env*.08
        buf.extend(struct.pack('<h',int(v*32767)))
    w.writeframes(buf)
with wave.open(str(AUDIO/'success.wav'),'wb') as w:
    w.setparams((1,2,rate,0,'NONE','not compressed'));buf=bytearray()
    for i in range(rate*2):
        t=i/rate;v=0
        for delay,f in [(0,523.25),(.16,659.25),(.32,783.99),(.52,1046.5)]:
            s=t-delay
            if s>=0:v+=math.sin(2*math.pi*f*s)*min(1,s/.008)*math.exp(-s*4)*.16
        buf.extend(struct.pack('<h',int(max(-1,min(1,v))*32767)))
    w.writeframes(buf)
print('Generated 12 original pixel sprites and 12 audio assets.')
