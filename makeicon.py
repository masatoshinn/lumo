from PIL import Image, ImageDraw
import os

img = Image.new('RGBA', (64, 64), (0, 0, 0, 0))
draw = ImageDraw.Draw(img)
draw.rectangle([0, 0, 63, 63], fill=(0, 80, 180))
draw.rectangle([8, 8, 56, 56], fill=(0, 120, 220))
draw.polygon([(32, 12), (48, 32), (32, 52), (16, 32)], fill=(255, 255, 255))
img.save(r'C:\Users\User\Pictures\lumo\src\Lumo.Editor\Assets\lumo_icon.png')
print('Icon created')
