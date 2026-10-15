using System.Globalization;
using MonthlyReportBot.Configuration;
namespace MonthlyReportBot.Models;
public sealed class Dialog
{
 public int? TaskId { get; set; }
 public string? FirstName { get; set; }
 public string? LastName { get; set; }
 public DateOnly? Date { get; set; }
 public DateOnly? Start { get; set; }
 public DateOnly? End { get; set; }
 public int Year { get; set; }
 public string Mode { get; set; } = "report";
 public string ReportType { get; set; } = "month";
 public int Page { get; set; }
}
public static class Dates
{
 public static readonly string[] Months = ["yanvar","fevral","mart","aprel","may","iyun","iyul","avgust","sentabr","oktabr","noyabr","dekabr"];
 public static DateOnly Parse(string text) => DateOnly.TryParseExact(text.Trim(),"dd.MM.yyyy",CultureInfo.InvariantCulture,DateTimeStyles.None,out var d) && d.Year>=2000 && d.Year<=2100 ? d : throw new UserInputException("Sanani dd.MM.yyyy shaklida kiriting (2000–2100).");
 public static (DateOnly Start, DateOnly End) Month(int year,int month) { if(year is <2000 or >2100 || month is <1 or >12) throw new UserInputException("Yil yoki oy noto‘g‘ri."); return (new(year,month,1),new(year,month,DateTime.DaysInMonth(year,month))); }
 public static void Validate(DateOnly start,DateOnly end) { if(start>end) throw new UserInputException("Boshlanish sanasi tugash sanasidan katta."); }
 public static string Period(DateOnly s,DateOnly e,bool monthly) => monthly ? $"{s.Year}-yil {Months[s.Month-1]} oyidagi" : $"{s.Year}-yil {s.Day}-{Months[s.Month-1]}dan {e.Year}-yil {e.Day}-{Months[e.Month-1]}gacha";
}
public static class Names
{
 public static string Short(string first,string last,bool spaced=false) => $"{StringInfo.GetNextTextElement(first)}.{(spaced ? " " : "")}{last}";
 public static string Safe(string value) => string.Concat(value.Where(c=>char.IsLetterOrDigit(c)||c=='-'||c=='_')).Truncate(60);
 public static string Truncate(this string s,int n) => s.Length<=n?s:s[..n];
}
