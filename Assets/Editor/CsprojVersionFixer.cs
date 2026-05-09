using UnityEditor;
using System.Text.RegularExpressions;

public class CsprojVersionFixer : AssetPostprocessor
{
    // 유니티가 VS Code용 .csproj 파일을 생성할 때마다 이 콜백이 실행되어 원본 텍스트를 가로챕니다.
    private static string OnGeneratedCSProject(string path, string content)
    {
        // 파일 내용 중 LangVersion 태그를 찾아 무조건 10.0(record struct 지원 버전)으로 뜯어고칩니다.
        return Regex.Replace(content, "<LangVersion>.*?</LangVersion>", "<LangVersion>10.0</LangVersion>");
    }
}