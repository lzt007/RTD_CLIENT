"""Optional single-page CJK bitmap font generator. Reads root configuration only."""
import pathlib,json,math
from PIL import Image,ImageFont,ImageDraw
ROOT=pathlib.Path(__file__).resolve().parents[1]
def path(s):
 p=pathlib.Path(s);return p if p.is_absolute() else ROOT/p
c=json.loads((ROOT/'rtd.config.json').read_text(encoding='utf-8-sig'));p=c['paths'];settings=c['font']
if not p.get('fontSource'):raise SystemExit('Set paths.fontSource in rtd.config.json to an appropriately licensed CJK TTF/TTC first.')
font=ImageFont.truetype(str(path(p['fontSource'])),settings['size']);size=settings['atlasSize'];cell=settings['size']+8
mapping=json.loads(path(p['translation']).read_text(encoding='utf-8-sig'));chars=set(chr(i) for i in range(32,127))
for k,v in mapping.items():chars.update(k);chars.update(v)
chars.update('中文示例字体');chars=sorted(ch for ch in chars if not ch.isspace() or ch==' ')
if any(ord(ch)>65535 for ch in chars):raise SystemExit('This legacy font supports BMP characters only; split supplementary characters deliberately.')
cols=size//cell
if math.ceil(len(chars)/cols)*cell>size:raise SystemExit('Glyphs exceed single atlas. Reduce subset/size; old UIFont does not automatically support multipage.')
im=Image.new('RGBA',(size,size),(255,255,255,0));draw=ImageDraw.Draw(im);lines=[f"{settings['size']}\t{settings['size']}\t{size}\t{size}"]
for i,ch in enumerate(chars):
 x=(i%cols)*cell;y=(i//cols)*cell;box=draw.textbbox((0,0),ch,font=font,anchor='lt');w=box[2]-box[0];h=box[3]-box[1]
 if w>cell or h>cell:raise SystemExit('Oversize glyph '+repr(ch))
 draw.text((x,y),ch,font=font,fill=(255,255,255,255),anchor='lt');advance=math.ceil(font.getlength(ch));lines.append(f'{ord(ch)}\t{x}\t{y}\t{w}\t{h}\t0\t0\t{advance}')
path(p['fontPng']).parent.mkdir(parents=True,exist_ok=True);im.save(path(p['fontPng']));path(p['fontGlyphs']).write_text('\n'.join(lines)+'\n',encoding='utf-8')
print('Generated',len(chars),'glyphs. Check font licensing and real-device metrics/baseline/texture memory before distributing.')
