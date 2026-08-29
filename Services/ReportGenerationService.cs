using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CloudAdvisor.Models.Domain;
using iTextSharp.text;
using iTextSharp.text.pdf;

namespace CloudAdvisor.Services
{
    public class ReportGenerationService : IReportGenerationService
    {
        public Task<byte[]> GeneratePdfReportAsync(AnalysisSession session)
        {
            if (session == null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            using (var memoryStream = new MemoryStream())
            {
                // Create Document
                var document = new Document(PageSize.A4, 36, 36, 54, 54);
                var writer = PdfWriter.GetInstance(document, memoryStream);
                
                // Add page number and footer branding page events
                writer.PageEvent = new ReportPageEventHelper();

                document.Open();

                // Define Colors
                var primaryColor = new BaseColor(59, 130, 246);   // Blue
                var secondaryColor = new BaseColor(30, 41, 59);  // Dark Slate
                var grayBg = new BaseColor(241, 245, 249);      // Light gray
                var textDark = new BaseColor(15, 23, 42);       // Dark gray/black
                var borderGray = new BaseColor(226, 232, 240);  // Light border

                // Define Fonts
                var coverTitleFont = new Font(Font.HELVETICA, 28, Font.BOLD, secondaryColor);
                var coverSubFont = new Font(Font.HELVETICA, 14, Font.NORMAL, new BaseColor(100, 116, 139));
                var sectionTitleFont = new Font(Font.HELVETICA, 16, Font.BOLD, secondaryColor);
                var tableHeaderFont = new Font(Font.HELVETICA, 10, Font.BOLD, BaseColor.White);
                var bodyFont = new Font(Font.HELVETICA, 10, Font.NORMAL, textDark);
                var bodyBoldFont = new Font(Font.HELVETICA, 10, Font.BOLD, textDark);
                var codeFont = new Font(Font.COURIER, 8, Font.NORMAL, textDark);
                
                // Badge fonts
                var requiredFont = new Font(Font.HELVETICA, 9, Font.BOLD, new BaseColor(220, 38, 38));   // Red
                var recommendedFont = new Font(Font.HELVETICA, 9, Font.BOLD, new BaseColor(217, 119, 6)); // Amber
                var optionalFont = new Font(Font.HELVETICA, 9, Font.BOLD, new BaseColor(21, 128, 61));   // Green

                // ==========================================
                // 1. COVER PAGE
                // ==========================================
                
                // Add logo placeholder / text logo
                var logoPara = new Paragraph("CloudAdvisor", new Font(Font.HELVETICA, 32, Font.BOLD, primaryColor));
                logoPara.Alignment = Element.ALIGN_CENTER;
                logoPara.SpacingBefore = 80f;
                document.Add(logoPara);

                var subtitlePara = new Paragraph("Intelligent Cloud Infrastructure Recommendation Engine", new Font(Font.HELVETICA, 12, Font.ITALIC, new BaseColor(71, 85, 105)));
                subtitlePara.Alignment = Element.ALIGN_CENTER;
                subtitlePara.SpacingAfter = 100f;
                document.Add(subtitlePara);

                // Main Title
                var mainTitle = new Paragraph("CLOUD DEPLOYMENT ARCHITECTURE REPORT", coverTitleFont);
                mainTitle.Alignment = Element.ALIGN_CENTER;
                mainTitle.SpacingAfter = 10f;
                document.Add(mainTitle);

                var projectSub = new Paragraph($"Project Name: {session.ProjectName}", coverSubFont);
                projectSub.Alignment = Element.ALIGN_CENTER;
                projectSub.SpacingAfter = 140f;
                document.Add(projectSub);

                // Meta box
                var metadataTable = new PdfPTable(1);
                metadataTable.WidthPercentage = 80;
                metadataTable.HorizontalAlignment = Element.ALIGN_CENTER;
                
                var cell = new PdfPCell(new Phrase(
                    $"Report Date: {DateTime.UtcNow:MMMM dd, yyyy HH:mm} UTC\n" +
                    $"Session ID: {session.SessionId}", bodyFont));
                cell.BackgroundColor = grayBg;
                cell.Padding = 15f;
                cell.BorderColor = borderGray;
                cell.HorizontalAlignment = Element.ALIGN_LEFT;
                metadataTable.AddCell(cell);
                document.Add(metadataTable);

                // New page for contents
                document.NewPage();

                // ==========================================
                // 2. SECTION 1: PROJECT OVERVIEW
                // ==========================================
                var overviewHeader = new Paragraph("1. Project Overview", sectionTitleFont);
                overviewHeader.SpacingAfter = 15f;
                document.Add(overviewHeader);

                var overviewText = new Paragraph(
                    $"CloudAdvisor has conducted a static code analysis of the project \"{session.ProjectName}\". " +
                    $"The analysis targeted architectural decorators, configurations, database scopes, file inputs, " +
                    $"and background services using the Microsoft Roslyn AST compilation pipeline.\n\n" +
                    $"Analysis Parameters & High-Level Summary:", bodyFont);
                overviewText.SpacingAfter = 15f;
                document.Add(overviewText);

                // Overview stats table
                var statsTable = new PdfPTable(2);
                statsTable.WidthPercentage = 100;
                statsTable.SetWidths(new float[] { 40f, 60f });
                
                void AddStatsRow(string label, string val)
                {
                    var labelCell = new PdfPCell(new Phrase(label, bodyBoldFont)) { BackgroundColor = grayBg, Padding = 6f, BorderColor = borderGray };
                    var valCell = new PdfPCell(new Phrase(val, bodyFont)) { Padding = 6f, BorderColor = borderGray };
                    statsTable.AddCell(labelCell);
                    statsTable.AddCell(valCell);
                }

                AddStatsRow("Project Name", session.ProjectName);
                AddStatsRow("Analysis Timestamp", session.UploadedAt.ToString("g"));
                AddStatsRow("Database Context Detected", session.HasDatabase ? "Yes" : "No");
                AddStatsRow("Authentication Middleware Detected", session.HasAuthentication ? "Yes" : "No");
                AddStatsRow("File Upload/IO Handling Detected", session.HasFileHandling ? "Yes" : "No");
                AddStatsRow("REST API Controllers Detected", session.HasApiControllers ? "Yes" : "No");
                AddStatsRow("Background Services Detected", session.HasBackgroundServices ? "Yes" : "No");
                AddStatsRow("Caching Configuration Detected", session.HasCaching ? "Yes" : "No");
                AddStatsRow("Estimated Monthly Cloud Cost (USD)", $"${session.TotalCost:F2}");

                document.Add(statsTable);
                document.Add(new Paragraph("\n"));

                // ==========================================
                // 3. SECTION 2: DETECTED ARCHITECTURAL FEATURES
                // ==========================================
                var featuresHeader = new Paragraph("2. Detected Architectural Features", sectionTitleFont);
                featuresHeader.SpacingBefore = 10f;
                featuresHeader.SpacingAfter = 15f;
                document.Add(featuresHeader);

                if (session.ExtractedFeatures == null || session.ExtractedFeatures.Count == 0)
                {
                    document.Add(new Paragraph("No specific custom code features were detected. CloudAdvisor default application templates apply.", bodyFont));
                }
                else
                {
                    var featuresTable = new PdfPTable(4);
                    featuresTable.WidthPercentage = 100;
                    featuresTable.SetWidths(new float[] { 22f, 25f, 33f, 20f });

                    string[] headers = { "Feature Type", "Name", "File Path", "Line" };
                    foreach (var h in headers)
                    {
                        var hCell = new PdfPCell(new Phrase(h, tableHeaderFont))
                        {
                            BackgroundColor = secondaryColor,
                            HorizontalAlignment = Element.ALIGN_LEFT,
                            Padding = 8f,
                            Border = PdfPCell.NO_BORDER
                        };
                        featuresTable.AddCell(hCell);
                    }

                    foreach (var f in session.ExtractedFeatures)
                    {
                        featuresTable.AddCell(new PdfPCell(new Phrase(f.FeatureType.ToString(), bodyBoldFont)) { Padding = 6f, BorderColor = borderGray });
                        featuresTable.AddCell(new PdfPCell(new Phrase(f.FeatureName, bodyFont)) { Padding = 6f, BorderColor = borderGray });
                        featuresTable.AddCell(new PdfPCell(new Phrase(f.FilePath, codeFont)) { Padding = 6f, BorderColor = borderGray });
                        featuresTable.AddCell(new PdfPCell(new Phrase(f.LineNumber.ToString(), bodyFont)) { Padding = 6f, BorderColor = borderGray });
                    }

                    document.Add(featuresTable);
                }

                document.Add(new Paragraph("\n"));

                // ==========================================
                // 4. SECTION 3: AWS SERVICE RECOMMENDATIONS
                // ==========================================
                var recsHeader = new Paragraph("3. AWS Service Infrastructure Recommendations", sectionTitleFont);
                recsHeader.SpacingBefore = 10f;
                recsHeader.SpacingAfter = 15f;
                document.Add(recsHeader);

                if (session.Recommendations == null || session.Recommendations.Count == 0)
                {
                    document.Add(new Paragraph("No recommendations generated.", bodyFont));
                }
                else
                {
                    // Loop recommendations and add card blocks
                    foreach (var rec in session.Recommendations)
                    {
                        var cardTable = new PdfPTable(1);
                        cardTable.WidthPercentage = 100;
                        cardTable.SpacingAfter = 12f;

                        // Header cell
                        var headerPhrase = new Phrase();
                        headerPhrase.Add(new Chunk($"AWS Service: {rec.AwsService}  |  Category: {rec.ServiceCategory}   ", bodyBoldFont));
                        
                        // Select priority badge
                        Font badgeFont = optionalFont;
                        string badgeText = "OPTIONAL";
                        if (rec.Priority == RecommendationPriority.Required)
                        {
                            badgeFont = requiredFont;
                            badgeText = "REQUIRED";
                        }
                        else if (rec.Priority == RecommendationPriority.Recommended)
                        {
                            badgeFont = recommendedFont;
                            badgeText = "RECOMMENDED";
                        }
                        headerPhrase.Add(new Chunk($"[{badgeText}]", badgeFont));

                        var cardHeader = new PdfPCell(headerPhrase)
                        {
                            BackgroundColor = grayBg,
                            Padding = 8f,
                            BorderColor = borderGray
                        };
                        cardTable.AddCell(cardHeader);

                        // Body cell
                        var cardBody = new PdfPCell(new Phrase(
                            $"Justification: {rec.Reason}\n" +
                            $"Triggering Feature: {rec.TriggeringFeature}", bodyFont))
                        {
                            Padding = 10f,
                            BorderColor = borderGray
                        };
                        cardTable.AddCell(cardBody);

                        document.Add(cardTable);
                    }
                }

                document.NewPage();

                // ==========================================
                // 5. SECTION 4: ESTIMATED MONTHLY DEPLOYMENT COSTS
                // ==========================================
                var costHeader = new Paragraph("4. Detailed Monthly Deployment Cost Estimates", sectionTitleFont);
                costHeader.SpacingBefore = 10f;
                costHeader.SpacingAfter = 15f;
                document.Add(costHeader);

                if (session.CostEstimates == null || session.CostEstimates.Count == 0)
                {
                    document.Add(new Paragraph("No cost estimates compiled for this session.", bodyFont));
                }
                else
                {
                    var costTable = new PdfPTable(3);
                    costTable.WidthPercentage = 100;
                    costTable.SetWidths(new float[] { 45f, 25f, 30f });

                    string[] costHeaders = { "AWS Service", "Instance/Service Tier", "Monthly Cost (USD)" };
                    foreach (var ch in costHeaders)
                    {
                        var chCell = new PdfPCell(new Phrase(ch, tableHeaderFont))
                        {
                            BackgroundColor = secondaryColor,
                            Padding = 8f,
                            Border = PdfPCell.NO_BORDER
                        };
                        costTable.AddCell(chCell);
                    }

                    foreach (var est in session.CostEstimates)
                    {
                        costTable.AddCell(new PdfPCell(new Phrase(est.ServiceName, bodyBoldFont)) { Padding = 6f, BorderColor = borderGray });
                        costTable.AddCell(new PdfPCell(new Phrase(est.Tier, bodyFont)) { Padding = 6f, BorderColor = borderGray });
                        costTable.AddCell(new PdfPCell(new Phrase($"${est.MonthlyCostUSD:F2}", bodyFont)) { Padding = 6f, BorderColor = borderGray });
                    }

                    // Total Row
                    var totalLabelCell = new PdfPCell(new Phrase("Total Estimated Monthly Deployment Cost", bodyBoldFont))
                    {
                        Colspan = 2,
                        BackgroundColor = grayBg,
                        Padding = 8f,
                        BorderColor = borderGray
                    };
                    var totalCostCell = new PdfPCell(new Phrase($"${session.TotalCost:F2}", new Font(Font.HELVETICA, 11, Font.BOLD, primaryColor)))
                    {
                        BackgroundColor = grayBg,
                        Padding = 8f,
                        BorderColor = borderGray
                    };
                    
                    costTable.AddCell(totalLabelCell);
                    costTable.AddCell(totalCostCell);

                    document.Add(costTable);
                }

                document.Add(new Paragraph("\n"));

                // Disclaimer note
                var disclaimerBox = new PdfPTable(1);
                disclaimerBox.WidthPercentage = 100;
                var disclaimerCell = new PdfPCell(new Phrase(
                    "IMPORTANT DISCLAIMER:\n" +
                    "Estimates are calculated using predefined AWS pricing configurations under CloudAdvisor rules. " +
                    "Actual deployment expenses on AWS will depend on runtime execution (CPU usage, database storage, S3 bandwidth, Cognito active users count). " +
                    "This document serves as an academic design blueprint and planning advisory for university grading project purposes.", 
                    new Font(Font.HELVETICA, 8, Font.ITALIC, new BaseColor(100, 116, 139))))
                {
                    Padding = 10f,
                    BorderColor = borderGray,
                    BackgroundColor = grayBg
                };
                disclaimerBox.AddCell(disclaimerCell);
                document.Add(disclaimerBox);

                document.Close();
                return Task.FromResult(memoryStream.ToArray());
            }
        }

        private class ReportPageEventHelper : PdfPageEventHelper
        {
            public override void OnEndPage(PdfWriter writer, Document document)
            {
                // No header or footer on the cover page
                if (writer.PageNumber == 1) return;

                PdfContentByte cb = writer.DirectContent;
                cb.BeginText();

                BaseFont bf = BaseFont.CreateFont(BaseFont.HELVETICA, BaseFont.CP1252, BaseFont.NOT_EMBEDDED);
                cb.SetFontAndSize(bf, 8);
                cb.SetColorFill(new BaseColor(148, 163, 184)); // gray-400

                // Footer left: branding
                cb.ShowTextAligned(PdfContentByte.ALIGN_LEFT, "CloudAdvisor", 36, 20, 0);

                // Footer right: page numbers
                cb.ShowTextAligned(PdfContentByte.ALIGN_RIGHT, $"Page {writer.PageNumber}", document.PageSize.Width - 36, 20, 0);

                cb.EndText();
            }
        }
    }
}
