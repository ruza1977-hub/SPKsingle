using System.Text;
namespace SistemPengurusanKehadiran.Services;
public static class CsvUtil
{
    public static List<Dictionary<string,string>> Read(Stream stream)
    {
        using var sr=new StreamReader(stream,Encoding.UTF8,true,leaveOpen:true);var text=sr.ReadToEnd().Replace("\r\n","\n").Replace('\r','\n');var rows=Parse(text);if(rows.Count==0)return[];var headers=rows[0].Select(Normalize).ToArray();var result=new List<Dictionary<string,string>>();foreach(var row in rows.Skip(1)){var d=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);for(int i=0;i<headers.Length&&i<row.Count;i++)if(!string.IsNullOrWhiteSpace(headers[i]))d[headers[i]]=row[i].Trim();if(d.Values.Any(v=>!string.IsNullOrWhiteSpace(v)))result.Add(d);}return result;
    }
    private static string Normalize(string s)=>s.Trim().Trim('\ufeff').ToLowerInvariant().Replace(" ","_");
    private static List<List<string>> Parse(string text){var all=new List<List<string>>();var row=new List<string>();var cell=new StringBuilder();bool q=false;for(int i=0;i<text.Length;i++){char ch=text[i];if(ch=='"'){if(q&&i+1<text.Length&&text[i+1]=='"'){cell.Append('"');i++;}else q=!q;}else if(ch==','&&!q){row.Add(cell.ToString());cell.Clear();}else if(ch=='\n'&&!q){row.Add(cell.ToString());cell.Clear();all.Add(row);row=new();}else cell.Append(ch);}if(cell.Length>0||row.Count>0){row.Add(cell.ToString());all.Add(row);}return all;}
}
