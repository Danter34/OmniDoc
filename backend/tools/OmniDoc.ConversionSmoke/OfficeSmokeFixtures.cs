using System.IO.Compression;
using System.Text;

internal static class OfficeSmokeFixtures
{
    private const string PackageNs = "http://schemas.openxmlformats.org/package/2006/relationships";
    private const string RelNs = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

    internal static byte[] Word()
    {
        using var buffer = new MemoryStream();
        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, true))
        {
            Add(zip, "[Content_Types].xml", Types("<Override PartName=\"/word/document.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml\"/>"));
            Add(zip, "_rels/.rels", Relationships($"<Relationship Id=\"rId1\" Type=\"{RelNs}/officeDocument\" Target=\"word/document.xml\"/>"));
            Add(zip, "word/document.xml", """
                <w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main">
                  <w:body>
                    <w:p><w:r><w:t>OmniDoc bằng chứng Word trang một</w:t></w:r></w:p>
                    <w:p><w:r><w:br w:type="page"/></w:r></w:p>
                    <w:p><w:r><w:t>OmniDoc bằng chứng Word trang hai</w:t></w:r></w:p>
                    <w:sectPr><w:pgSz w:w="11906" w:h="16838"/><w:pgMar w:top="1134" w:right="1134" w:bottom="1134" w:left="1134"/></w:sectPr>
                  </w:body>
                </w:document>
                """);
        }
        return buffer.ToArray();
    }

    internal static byte[] Slides(bool wide)
    {
        using var buffer = new MemoryStream();
        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, true))
        {
            var overrides = "<Override PartName=\"/ppt/presentation.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.presentationml.presentation.main+xml\"/>";
            overrides += string.Concat(Enumerable.Range(1, 3).Select(i => $"<Override PartName=\"/ppt/slides/slide{i}.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.presentationml.slide+xml\"/>"));
            overrides += "<Override PartName=\"/ppt/notesSlides/notesSlide1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.presentationml.notesSlide+xml\"/>";
            Add(zip, "[Content_Types].xml", Types(overrides));
            Add(zip, "_rels/.rels", Relationships($"<Relationship Id=\"rId1\" Type=\"{RelNs}/officeDocument\" Target=\"ppt/presentation.xml\"/>"));
            Add(zip, "ppt/presentation.xml", $"""
                <p:presentation xmlns:p="http://schemas.openxmlformats.org/presentationml/2006/main" xmlns:r="{RelNs}">
                  <p:sldIdLst><p:sldId id="256" r:id="rId1"/><p:sldId id="257" r:id="rId2"/><p:sldId id="258" r:id="rId3"/></p:sldIdLst>
                  <p:sldSz cx="{(wide ? 12192000 : 9144000)}" cy="6858000"/><p:notesSz cx="6858000" cy="9144000"/>
                </p:presentation>
                """);
            Add(zip, "ppt/_rels/presentation.xml.rels", Relationships(string.Concat(Enumerable.Range(1, 3).Select(i => $"<Relationship Id=\"rId{i}\" Type=\"{RelNs}/slide\" Target=\"slides/slide{i}.xml\"/>"))));
            for (var i = 1; i <= 3; i++)
            {
                var text = i == 2 ? "HIDDEN_SECRET" : $"OmniDoc slide {i}";
                Add(zip, $"ppt/slides/slide{i}.xml", $"""
                    <p:sld xmlns:p="http://schemas.openxmlformats.org/presentationml/2006/main" xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main" show="{(i == 2 ? "0" : "1")}">
                      <p:cSld>{ShapeTree(text)}</p:cSld><p:clrMapOvr><a:masterClrMapping/></p:clrMapOvr>
                    </p:sld>
                    """);
            }
            Add(zip, "ppt/slides/_rels/slide1.xml.rels", Relationships($"<Relationship Id=\"rId1\" Type=\"{RelNs}/notesSlide\" Target=\"../notesSlides/notesSlide1.xml\"/>"));
            Add(zip, "ppt/notesSlides/notesSlide1.xml", $"""
                <p:notes xmlns:p="http://schemas.openxmlformats.org/presentationml/2006/main" xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main"><p:cSld>{ShapeTree("SPEAKER_SECRET", notes: true)}</p:cSld></p:notes>
                """);
            Add(zip, "ppt/notesSlides/_rels/notesSlide1.xml.rels", Relationships($"<Relationship Id=\"rId1\" Type=\"{RelNs}/slide\" Target=\"../slides/slide1.xml\"/>"));
        }
        return buffer.ToArray();
    }

    private static string ShapeTree(string text, bool notes = false) => $"""
        <p:spTree>
          <p:nvGrpSpPr><p:cNvPr id="1" name=""/><p:cNvGrpSpPr/><p:nvPr/></p:nvGrpSpPr>
          <p:grpSpPr><a:xfrm><a:off x="0" y="0"/><a:ext cx="0" cy="0"/><a:chOff x="0" y="0"/><a:chExt cx="0" cy="0"/></a:xfrm></p:grpSpPr>
          <p:sp><p:nvSpPr><p:cNvPr id="2" name="Evidence"/><p:cNvSpPr txBox="1"/><p:nvPr>{(notes ? "<p:ph type=\"body\" idx=\"1\"/>" : "")}</p:nvPr></p:nvSpPr>
            <p:spPr><a:xfrm><a:off x="914400" y="914400"/><a:ext cx="7315200" cy="1828800"/></a:xfrm><a:prstGeom prst="rect"><a:avLst/></a:prstGeom></p:spPr>
            <p:txBody><a:bodyPr/><a:lstStyle/><a:p><a:r><a:rPr lang="en-US" sz="2400"/><a:t>{text}</a:t></a:r></a:p></p:txBody>
          </p:sp>
        </p:spTree>
        """;

    private static string Types(string entries) => $"<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/>{entries}</Types>";
    private static string Relationships(string entries) => $"<Relationships xmlns=\"{PackageNs}\">{entries}</Relationships>";
    private static void Add(ZipArchive zip, string name, string xml)
    {
        using var writer = new StreamWriter(zip.CreateEntry(name).Open(), new UTF8Encoding(false));
        writer.Write(xml);
    }
}
