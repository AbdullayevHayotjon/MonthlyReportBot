using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.Extensions.Options;
using MonthlyReportBot.Configuration;
using MonthlyReportBot.Data.Entities;
using MonthlyReportBot.Models;
using MonthlyReportBot.Services.Images;
using SkiaSharp;
using A=DocumentFormat.OpenXml.Drawing;
using DW=DocumentFormat.OpenXml.Drawing.Wordprocessing;
using PIC=DocumentFormat.OpenXml.Drawing.Pictures;
namespace MonthlyReportBot.Services.Reports;
public sealed class WordReportGenerator(IOptions<BotOptions> options,FileChanges files)
{
 readonly BotOptions o=options.Value;
 public byte[] Generate(Employee user,DateOnly start,DateOnly end,bool monthly,IReadOnlyList<WorkTask> tasks)
 {
  Dates.Validate(start,end);
  if(tasks.Count==0)throw new UserInputException("Tanlangan davrda task yo‘q.");
  if(tasks.Any(t=>t.UserId!=user.Id||t.IsDraft||t.WorkDate<start||t.WorkDate>end))throw new InvalidOperationException("Report ownership/period violation");
  if(tasks.Count>o.MaxReportTasks||tasks.Sum(t=>t.Images.Count)>o.MaxReportImages||tasks.Sum(t=>t.Images.Sum(i=>i.FileSize))>o.MaxReportBytes-1024*1024)throw new UserInputException("Hisobot limiti oshgan. Qisqaroq sana oralig‘ini tanlang.");
  using var ms=new MemoryStream();using(var source=File.OpenRead(o.TemplatePath))source.CopyTo(ms);
  using(var doc=WordprocessingDocument.Open(ms,true))
  {
   var main=doc.MainDocumentPart??throw new InvalidDataException("Template main part missing");
   var body=main.Document.Body??throw new InvalidDataException("Template body missing");
   var title=body.Elements<Paragraph>().First();
   SetText(title,$"{o.OrganizationName} {o.DepartmentName} {user.PositionType} {Names.Short(user.FirstName,user.LastName)}ning {Dates.Period(start,end,monthly)} bajargan ishlari yuzasidan");
   var signature=body.Elements<Paragraph>().First(p=>p.InnerText.Contains("Topshirdi"));
   var signatureStyle=signature.Descendants<RunProperties>().FirstOrDefault()?.CloneNode(true);
   SetText(signature,"Topshirdi:");
   var run=new Run();if(signatureStyle!=null)run.Append(signatureStyle);
   for(int i=0;i<8;i++)run.Append(new TabChar());
   run.Append(new Text(Names.Short(user.FirstName,user.LastName,true)));signature.Append(run);
   // Drop all example drawings and media parts, even if the supplied template was not sanitized.
   foreach(var d in body.Descendants<Drawing>().ToList())d.Remove();
   foreach(var p in main.ImageParts.ToList())main.DeletePart(p);
   var section=body.Elements<SectionProperties>().Last();
   foreach(var node in body.ChildElements.SkipWhile(x=>x!=signature).Skip(1).Where(x=>x!=section).ToList())node.Remove();
   var table=body.Elements<Table>().Single();var sample=(TableRow)table.Elements<TableRow>().First().CloneNode(true);
   table.RemoveAllChildren<TableRow>();
   // Restart existing Word numbering at 1 with a fresh instance, retaining the source list definition.
   var oldId=sample.Descendants<NumberingId>().FirstOrDefault()?.Val?.Value;
   int? newId=null;
   var numbering=main.NumberingDefinitionsPart?.Numbering;
   if(oldId!=null&&numbering!=null)
   {
    var old=numbering.Elements<NumberingInstance>().FirstOrDefault(n=>n.NumberID?.Value==oldId);
    if(old!=null){newId=numbering.Elements<NumberingInstance>().Max(n=>n.NumberID?.Value??0)+1;var inst=new NumberingInstance(new AbstractNumId{Val=old.AbstractNumId!.Val},new LevelOverride(new StartOverrideNumberingValue{Val=1}){LevelIndex=0}){NumberID=newId.Value};numbering.Append(inst);numbering.Save();}
   }
   int index=0;uint drawId=1;
   var ordered=tasks.OrderBy(t=>t.WorkDate).ThenBy(t=>t.CreatedAt).ThenBy(t=>t.Id).ToList();
   foreach(var task in ordered)
   {
    index++;var row=(TableRow)sample.CloneNode(true);var cell=row.Elements<TableCell>().Single();var p=cell.Elements<Paragraph>().First();
    foreach(var extra in cell.Elements<Paragraph>().Skip(1).ToList())extra.Remove();
    SetText(p,(newId==null?$"{index}. ":"")+task.Description);
    if(newId!=null)foreach(var n in p.Descendants<NumberingId>())n.Val=newId;
    else p.ParagraphProperties?.RemoveAllChildren<NumberingProperties>();
    row.TableRowProperties?.RemoveAllChildren<TableRowHeight>();table.Append(row);
   }
   var size=section.GetFirstChild<PageSize>()!;var margin=section.GetFirstChild<PageMargin>()!;
   long maxW=((long)size.Width!.Value-(margin.Left?.Value??1440)-(margin.Right?.Value??1440))*635;
   long maxH=((long)size.Height!.Value-(margin.Top?.Value??1440)-(margin.Bottom?.Value??1440)-400)*635;
   foreach(var task in ordered)foreach(var image in task.Images.OrderBy(i=>i.SortOrder).ThenBy(i=>i.Id))
   {
    if(image.TaskId!=task.Id)throw new InvalidOperationException("Image ownership violation");
    var path=files.Resolve(image.FilePath);using var stream=File.OpenRead(path);using var codec=SKCodec.Create(stream);
    if(codec==null)throw new InvalidDataException("Stored image is invalid");
    var factor=Math.Min((double)maxW/codec.Info.Width,(double)maxH/codec.Info.Height);
    long cx=(long)(codec.Info.Width*factor),cy=(long)(codec.Info.Height*factor);
    var part=main.AddImagePart(image.MimeType=="image/png"?ImagePartType.Png:ImagePartType.Jpeg);
    using(var input=File.OpenRead(path))part.FeedData(input);
    var drawing=ImageDrawing(main.GetIdOfPart(part),drawId++,cx,cy);
    var props=new ParagraphProperties(new KeepLines(),new SpacingBetweenLines{Before="120",After="120",Line="240",LineRule=LineSpacingRuleValues.Auto},new Justification{Val=JustificationValues.Center});
    body.InsertBefore(new Paragraph(props,new Run(drawing)),section);
   }
   doc.PackageProperties.Creator="";doc.PackageProperties.LastModifiedBy="";doc.PackageProperties.Title="Hisobot";
   main.Document.Save();
   var errors=new OpenXmlValidator(FileFormatVersions.Office2019).Validate(doc).Take(10).ToList();
   if(errors.Count>0)throw new InvalidDataException("Open XML validation: "+string.Join("; ",errors.Select(e=>e.Description)));
  }
  if(ms.Length>o.MaxReportBytes)throw new UserInputException("Word fayl hajmi limitdan oshdi.");return ms.ToArray();
 }
 static void SetText(Paragraph p,string text)
 {
  var props=p.GetFirstChild<ParagraphProperties>()?.CloneNode(true);
  var runProps=p.Descendants<RunProperties>().FirstOrDefault()?.CloneNode(true);
  p.RemoveAllChildren();if(props!=null)p.Append(props);
  var r=new Run();if(runProps!=null)r.Append(runProps);
  var lines=text.Replace("\r","").Split('\n');for(int i=0;i<lines.Length;i++){if(i>0)r.Append(new Break());r.Append(new Text(lines[i]){Space=SpaceProcessingModeValues.Preserve});}p.Append(r);
 }
 static Drawing ImageDrawing(string rel,uint id,long cx,long cy) => new(new DW.Inline(
  new DW.Extent{Cx=cx,Cy=cy},new DW.EffectExtent{LeftEdge=0,TopEdge=0,RightEdge=0,BottomEdge=0},
  new DW.DocProperties{Id=id,Name=$"Screenshot {id}",Description=$"Task rasmi {id}"},
  new DW.NonVisualGraphicFrameDrawingProperties(new A.GraphicFrameLocks{NoChangeAspect=true}),
  new A.Graphic(new A.GraphicData(new PIC.Picture(
   new PIC.NonVisualPictureProperties(new PIC.NonVisualDrawingProperties{Id=id,Name=$"Image {id}"},new PIC.NonVisualPictureDrawingProperties()),
   new PIC.BlipFill(new A.Blip{Embed=rel},new A.Stretch(new A.FillRectangle())),
   new PIC.ShapeProperties(new A.Transform2D(new A.Offset{X=0,Y=0},new A.Extents{Cx=cx,Cy=cy}),new A.PresetGeometry(new A.AdjustValueList()){Preset=A.ShapeTypeValues.Rectangle}))){Uri="http://schemas.openxmlformats.org/drawingml/2006/picture"}))
  {DistanceFromTop=0,DistanceFromBottom=0,DistanceFromLeft=0,DistanceFromRight=0});
}
