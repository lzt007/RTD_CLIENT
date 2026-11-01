"""Generate a single-page bold YouYuan bitmap atlas from actual translation values."""
import pathlib,json,math
from PIL import Image,ImageFont,ImageDraw
from fontTools.ttLib import TTFont
ROOT=pathlib.Path(__file__).resolve().parents[1]
def path(s):
 p=pathlib.Path(s);return p if p.is_absolute() else ROOT/p
c=json.loads((ROOT/'rtd.config.json').read_text(encoding='utf-8-sig'));p=c['paths'];cfg=c['font']
fontpath=path(p['fontSource']);font=ImageFont.truetype(str(fontpath),cfg['size']);cmap=TTFont(fontpath).getBestCmap()
m=json.loads(path(p['translation']).read_text(encoding='utf8'));chars=set(chr(i) for i in range(32,127))
for text in m.values():chars.update(ch for ch in text if not ch.isspace())
size=cfg['atlasSize'];height=cfg.get('atlasHeight',size);stroke=cfg.get('strokeWidth',1);cell=cfg['size']+10;cols=size//cell
fallback=ROOT.parent/'RTD_TRANSLATION/analysis/tw-apk/sharedassets1.assets_97_font.json'
old=json.loads(fallback.read_text(encoding='utf8'))['mFont'];glyphs={g['index']:g for g in old['mSaved']}
chars.update(chr(cp) for cp in glyphs);chars=sorted(chars,key=ord)
if math.ceil(len(chars)/cols)*cell>height:raise ValueError('Atlas capacity exceeded')
oldimage=Image.open(ROOT.parent/'RTD_TRANSLATION/analysis/tw-apk/sharedassets1.assets_41.png').convert('RGBA')
im=Image.new('RGBA',(size,height),(255,255,255,0));draw=ImageDraw.Draw(im);ascent,descent=font.getmetrics();nominal=round(cfg['size']/cfg.get('displayScale',1.0));lines=[f"{nominal}\t{ascent}\t{size}\t{height}"];fallback_chars=[]
for i,ch in enumerate(chars):
 x=i%cols*cell+2;y=i//cols*cell+2
 if ord(ch) in cmap:
  left,top,right,bottom=font.getbbox(ch,anchor='ls',stroke_width=stroke);w=right-left;h=bottom-top
  if w>cell-4 or h>cell-4:raise ValueError('Oversize glyph '+repr(ch))
  draw.text((x-left,y-top),ch,font=font,anchor='ls',fill=(255,255,255,255),stroke_width=stroke,stroke_fill=(255,255,255,255))
  ox=left;oy=ascent+top;advance=math.ceil(font.getlength(ch))+stroke
 else:
  g=glyphs.get(ord(ch))
  if g is None:raise ValueError('Missing source glyph '+repr(ch))
  scale=cfg['size']/old['mSize'];w=max(1,round(g['width']*scale));h=max(1,round(g['height']*scale));tile=oldimage.crop((g['x'],g['y'],g['x']+g['width'],g['y']+g['height'])).resize((w,h),Image.Resampling.LANCZOS);im.paste(tile,(x,y));ox=round(g['offsetX']*scale);oy=round(g['offsetY']*scale);advance=round(g['advance']*scale);fallback_chars.append(ch)
 lines.append('\t'.join(map(str,[ord(ch),x,y,w,h,ox,oy,advance])))
path(p['fontPng']).parent.mkdir(parents=True,exist_ok=True);im.save(path(p['fontPng']));path(p['fontGlyphs']).write_text('\n'.join(lines)+'\n',encoding='utf8')
preview=Image.new('RGB',(1600,260),'#20242a');d=ImageDraw.Draw(preview)
for i,text in enumerate(['幼圆加粗高清字体 生命值 攻击力 回复力','火属性伤害增加50% 技能效果不可叠加','队伍全员发动攻击！1234567890']):d.text((20,10+i*80),text,font=font,fill='white',stroke_width=stroke)
(ROOT/'dist').mkdir(exist_ok=True);preview.save(ROOT/'dist/youyuan-bold-preview.png')
print(json.dumps({'glyphs':len(chars),'size':cfg['size'],'atlas':[size,height],'fallback_symbols':fallback_chars},ensure_ascii=True))


