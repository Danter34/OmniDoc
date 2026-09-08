using System.IO.Compression;
using System.Text;

internal static class SampleSources
{
    // Original fictional content, safe to commit and share. All amounts are illustrative.
    internal const string Report = """
        # NORTHSTAR — BÁO CÁO VẬN HÀNH QUÝ III/2026
        Tài liệu minh họa do dự án OmniDoc biên soạn. Northstar là doanh nghiệp giả định.
        ## Kết quả kinh doanh
        Doanh thu quý III đạt 12 tỷ đồng, tăng 20% so với quý II (10 tỷ đồng).
        Nhóm dịch vụ đóng góp 8 tỷ đồng; nhóm phần mềm đóng góp 4 tỷ đồng.
        Tỷ lệ hồ sơ mua sắm xử lý đúng hạn đạt 92%. Mục tiêu quý IV là 98%.
        ## Ngân sách cải tiến
        Ngân sách chương trình số hóa quy trình mua sắm là 1,2 tỷ đồng.
        Phân bổ: phần mềm 600 triệu đồng, đào tạo 200 triệu đồng, tích hợp 300 triệu đồng và dự phòng 100 triệu đồng.
        Giám đốc tài chính chịu trách nhiệm theo dõi ngân sách mỗi tháng.
        ## Định hướng quý IV
        Chuẩn hóa quy trình phê duyệt và thí điểm tại hai bộ phận: Vận hành và Tài chính.
        Đo lường theo tỷ lệ hồ sơ đúng hạn, không theo số lượng tài khoản đăng ký.
        Nguồn đối soát: Quy trình mua sắm và Kế hoạch triển khai Northstar đi kèm.
        """;
    private const string Rel = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static string Escape(string text) => System.Net.WebUtility.HtmlEncode(text);

    internal static byte[] Word()
    {
        var pages = new[]
        {
            new[] { "NORTHSTAR — QUY TRÌNH MUA SẮM", "Tài liệu minh họa OmniDoc. Phiên bản 1.0, tháng 09/2026.",
                "1. Phê duyệt và thời hạn", "Mọi đề nghị mua sắm cần được trưởng bộ phận phê duyệt trước khi chuyển đến phòng tài chính.",
                "Phòng tài chính phản hồi trong vòng 03 ngày làm việc kể từ khi nhận đủ hồ sơ hợp lệ.",
                "Hồ sơ gồm mô tả nhu cầu, dự toán chi phí, báo giá và thời hạn dự kiến." },
            new[] { "2. Phân quyền ngân sách", "Đề nghị từ 100 triệu đồng trở lên cần được giám đốc tài chính phê duyệt bổ sung.",
                "Ngân sách số hóa là 1,2 tỷ đồng theo Báo cáo vận hành quý III. Chi phí ngoài ngân sách phải có giải trình riêng.",
                "3. Theo dõi và lưu trữ", "Bộ phận Vận hành cập nhật tình trạng hồ sơ hằng tuần. Tài chính đối soát ngân sách hằng tháng.",
                "Mục tiêu quý IV: 98% hồ sơ được xử lý đúng hạn. Lưu quyết định phê duyệt cùng báo giá để đối soát." }
        };
        var body = string.Join("<w:p><w:r><w:br w:type=\"page\"/></w:r></w:p>", pages.Select(page => string.Concat(page.Select(line => $"<w:p><w:r><w:t>{Escape(line)}</w:t></w:r></w:p>"))));
        return Zip(new Dictionary<string, string>
        {
            ["[Content_Types].xml"] = Types("<Override PartName=\"/word/document.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml\"/>"),
            ["_rels/.rels"] = Relationships($"<Relationship Id=\"rId1\" Type=\"{Rel}/officeDocument\" Target=\"word/document.xml\"/>"),
            ["word/document.xml"] = $"""
                <w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main"><w:body>{body}
                <w:sectPr><w:pgSz w:w="11906" w:h="16838"/><w:pgMar w:top="1134" w:right="1134" w:bottom="1134" w:left="1134"/></w:sectPr>
                </w:body></w:document>
                """
        });
    }

    internal static byte[] Slides()
    {
        var slides = new[]
        {
            new[] { "NORTHSTAR / KẾ HOẠCH TRIỂN KHAI", "Chương trình số hóa mua sắm — Quý IV/2026", "Ngân sách: 1,2 tỷ đồng. Chủ trì: Giám đốc tài chính.", "Dữ liệu giả định do OmniDoc biên soạn." },
            new[] { "LỘ TRÌNH VÀ NGƯỜI PHỤ TRÁCH", "Tháng 10: Vận hành chuẩn hóa biểu mẫu và đào tạo.", "Tháng 11: Tài chính thí điểm tại Vận hành và Tài chính.", "Tháng 12: Ban dự án đánh giá và quyết định mở rộng." },
            new[] { "TIÊU CHÍ NGHIỆM THU", "98% hồ sơ được xử lý đúng hạn trong tháng 12.", "Tài chính phản hồi trong 03 ngày làm việc.", "100% quyết định phê duyệt có chứng từ đối soát.", "Đối chiếu Báo cáo vận hành và Quy trình mua sắm." }
        };
        var entries = new Dictionary<string, string>
        {
            ["[Content_Types].xml"] = Types("<Override PartName=\"/ppt/presentation.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.presentationml.presentation.main+xml\"/>" + string.Concat(Enumerable.Range(1, slides.Length).Select(i => $"<Override PartName=\"/ppt/slides/slide{i}.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.presentationml.slide+xml\"/>"))),
            ["_rels/.rels"] = Relationships($"<Relationship Id=\"rId1\" Type=\"{Rel}/officeDocument\" Target=\"ppt/presentation.xml\"/>"),
            ["ppt/presentation.xml"] = $"""
                <p:presentation xmlns:p="http://schemas.openxmlformats.org/presentationml/2006/main" xmlns:r="{Rel}">
                <p:sldIdLst>{string.Concat(Enumerable.Range(1, slides.Length).Select(i => $"<p:sldId id=\"{255 + i}\" r:id=\"rId{i}\"/>"))}</p:sldIdLst>
                <p:sldSz cx="12192000" cy="6858000"/><p:notesSz cx="6858000" cy="9144000"/></p:presentation>
                """,
            ["ppt/_rels/presentation.xml.rels"] = Relationships(string.Concat(Enumerable.Range(1, slides.Length).Select(i => $"<Relationship Id=\"rId{i}\" Type=\"{Rel}/slide\" Target=\"slides/slide{i}.xml\"/>")))
        };
        for (var i = 0; i < slides.Length; i++)
        {
            var paragraphs = string.Concat(slides[i].Select((line, n) => $"<a:p><a:r><a:rPr lang=\"vi-VN\" sz=\"{(n == 0 ? 2800 : 2000)}\"/><a:t>{Escape(line)}</a:t></a:r></a:p>"));
            entries[$"ppt/slides/slide{i + 1}.xml"] = $"""
                <p:sld xmlns:p="http://schemas.openxmlformats.org/presentationml/2006/main" xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main">
                <p:cSld><p:spTree><p:nvGrpSpPr><p:cNvPr id="1" name=""/><p:cNvGrpSpPr/><p:nvPr/></p:nvGrpSpPr><p:grpSpPr/>
                <p:sp><p:nvSpPr><p:cNvPr id="2" name="Evidence"/><p:cNvSpPr/><p:nvPr/></p:nvSpPr>
                <p:spPr><a:xfrm><a:off x="600000" y="600000"/><a:ext cx="10992000" cy="5658000"/></a:xfrm><a:prstGeom prst="rect"><a:avLst/></a:prstGeom></p:spPr>
                <p:txBody><a:bodyPr wrap="square"/><a:lstStyle/>{paragraphs}</p:txBody></p:sp>
                </p:spTree></p:cSld><p:clrMapOvr><a:masterClrMapping/></p:clrMapOvr></p:sld>
                """;
        }
        return Zip(entries);
    }

    private static string Types(string overrides) => $"<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/>{overrides}</Types>";
    private static string Relationships(string content) => $"<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">{content}</Relationships>";
    private static byte[] Zip(Dictionary<string, string> entries)
    {
        using var buffer = new MemoryStream();
        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, true))
            foreach (var (name, xml) in entries)
            {
                using var writer = new StreamWriter(zip.CreateEntry(name).Open(), new UTF8Encoding(false));
                writer.Write(xml);
            }
        return buffer.ToArray();
    }
}
