from pathlib import Path
import posixpath
from xml.sax.saxutils import escape
from zipfile import ZipFile, ZIP_DEFLATED, ZIP_STORED

root = Path(__file__).resolve().parent / 'fixtures'
root.mkdir(exist_ok=True)

objects = [b'<< /Type /Catalog /Pages 2 0 R >>', b'']
pages = []
for number in range(1, 25):
    page_id = len(objects) + 1
    stream_id = page_id + 1
    pages.append(page_id)
    color = f'{number / 25:.3f} 0.35 0.65'
    stream = f'{color} rg 0 0 600 900 re f\n1 1 1 rg BT /F1 40 Tf 65 760 Td (Synthetic PDF page {number}) Tj ET'.encode('ascii')
    objects.append(f'<< /Type /Page /Parent 2 0 R /MediaBox [0 0 600 900] /Resources << /Font << /F1 << /Type /Font /Subtype /Type1 /BaseFont /Helvetica >> >> >> /Contents {stream_id} 0 R >>'.encode('ascii'))
    objects.append(b'<< /Length ' + str(len(stream)).encode() + b' >>\nstream\n' + stream + b'\nendstream')
objects[1] = f'<< /Type /Pages /Count {len(pages)} /Kids [{' '.join(f'{page} 0 R' for page in pages)}] >>'.encode()
data = bytearray(b'%PDF-1.4\n')
offsets = [0]
for index, obj in enumerate(objects, 1):
    offsets.append(len(data))
    data.extend(f'{index} 0 obj\n'.encode() + obj + b'\nendobj\n')
xref = len(data)
data.extend(f'xref\n0 {len(offsets)}\n0000000000 65535 f \n'.encode())
for offset in offsets[1:]:
    data.extend(f'{offset:010d} 00000 n \n'.encode())
data.extend(f'trailer\n<< /Size {len(offsets)} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n'.encode())
(root / 'pages.pdf').write_bytes(data)

def epub(name, package, nav, chapter, ncx=False):
    package_dir = posixpath.dirname(package)
    nav_href = posixpath.relpath(nav, package_dir)
    chapter_href = posixpath.relpath(chapter, package_dir).replace(' ', '%20')
    target = posixpath.relpath(chapter, posixpath.dirname(nav)).replace(' ', '%20') + '#target'
    cover = posixpath.join(package_dir, 'cover.xhtml')
    title = escape(name)
    nav_item = '<item id="nav" href="' + nav_href + '" media-type="' + ('application/x-dtbncx+xml' if ncx else 'application/xhtml+xml') + '"' + ('' if ncx else ' properties="nav"') + '/>'
    opf = f'<?xml version="1.0"?><package xmlns="http://www.idpf.org/2007/opf" version="{2 if ncx else 3}.0" unique-identifier="id"><metadata xmlns:dc="http://purl.org/dc/elements/1.1/"><dc:identifier id="id">reader-audit-{title}</dc:identifier><dc:title>{title}</dc:title><dc:language>en</dc:language></metadata><manifest><item id="cover" href="cover.xhtml" media-type="application/xhtml+xml"/><item id="chapter" href="{chapter_href}" media-type="application/xhtml+xml"/>{nav_item}</manifest><spine toc="nav"><itemref idref="cover"/><itemref idref="chapter"/></spine></package>'
    if ncx:
        navigation = f'<?xml version="1.0"?><ncx xmlns="http://www.daisy.org/z3986/2005/ncx/" version="2005-1"><head/><docTitle><text>{title}</text></docTitle><navMap><navPoint id="target" playOrder="1"><navLabel><text>Target chapter</text></navLabel><content src="{target}"/></navPoint></navMap></ncx>'
    else:
        navigation = f'<html xmlns="http://www.w3.org/1999/xhtml" xmlns:epub="http://www.idpf.org/2007/ops"><head><title>Contents</title></head><body><nav epub:type="toc"><ol><li><a href="{target}">Target chapter</a></li></ol></nav></body></html>'
    def xhtml(text):
        return f'<html xmlns="http://www.w3.org/1999/xhtml"><head><title>{title}</title></head><body><h1 id="target">{text}</h1><p>This is a synthetic regression fixture. No copyrighted book content is included.</p></body></html>'
    with ZipFile(root / f'{name}.epub', 'w') as archive:
        archive.writestr('mimetype', 'application/epub+zip', compress_type=ZIP_STORED)
        for path, content in {
            'META-INF/container.xml': f'<container version="1.0" xmlns="urn:oasis:names:tc:opendocument:xmlns:container"><rootfiles><rootfile full-path="{package}" media-type="application/oebps-package+xml"/></rootfiles></container>',
            package: opf, nav: navigation, cover: xhtml('Initial cover'), chapter: xhtml('TARGET CHAPTER')
        }.items():
            archive.writestr(path, content, compress_type=ZIP_DEFLATED)

epub('nested-nav', 'OEBPS/content.opf', 'OEBPS/Text/nav.xhtml', 'OEBPS/Text/chapter.xhtml')
epub('parent-links', 'OEBPS/content.opf', 'OEBPS/Nav/Sub/toc.ncx', 'OEBPS/Text/chapter.xhtml', True)
epub('parent-package', 'OEBPS/Package/content.opf', 'OEBPS/Navigation/nav.xhtml', 'OEBPS/Text/chapter.xhtml')
epub('escaped-name', 'OEBPS/content.opf', 'OEBPS/Text/nav.xhtml', 'OEBPS/Text/A Book.xhtml')
epub('standard-ncx', 'OEBPS/content.opf', 'OEBPS/toc.ncx', 'OEBPS/Text/chapter.xhtml', True)
print('Created one PDF and five EPUB fixtures.')
