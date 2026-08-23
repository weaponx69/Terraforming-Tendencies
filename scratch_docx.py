import zipfile
import xml.etree.ElementTree as ET
import sys

def docx_to_text(path):
    try:
        with zipfile.ZipFile(path) as docx:
            xml_content = docx.read('word/document.xml')
            tree = ET.fromstring(xml_content)
            
            # Namespace for Word XML
            ns = {'w': 'http://schemas.openxmlformats.org/wordprocessingml/2006/main'}
            
            # Extract paragraphs
            text = []
            for para in tree.findall('.//w:p', ns):
                para_text = "".join(node.text for node in para.findall('.//w:t', ns) if node.text)
                if para_text:
                    text.append(para_text)
            return "\n\n".join(text)
    except Exception as e:
        return str(e)

print("--- Terraforming-Tendencies-GDD.docx ---")
print(docx_to_text("Terraforming-Tendencies-GDD.docx")[:500])

print("--- GDD.docx ---")
print(docx_to_text("GDD.docx")[:500])
