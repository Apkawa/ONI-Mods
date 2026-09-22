# Draft: 2026-09-22_workshop-vdf

Я прорабатываю рабочий процесс для публикации модов

сначала настроить сборку чтобы в папку релизного мода попадал vdf
Ориентировочный пример
```xml
<Target Name="GenerateSteamVdf" AfterTargets="Build" Condition="'$(Configuration)' == 'Release'">
  <PropertyGroup>
    <!-- Пути к файлам -->
    <ReadmePath>$(ProjectDir)README.md</ReadmePath>
    <VdfOutputPath>$(TargetDir)workshop_build.vdf</VdfOutputPath>
    <ModId>ВАШ_MOD_ID</ModId>
    <PreviewName>preview.png</PreviewName>
     <Visiblity>2</Visiblity>
  </PropertyGroup>

  <!-- Считываем README.md (TODO сконвертировать из md в steam bbcode)-->
  <ReadLinesFromFile File="$(ReadmePath)">
    <Output TaskParameter="Lines" ItemName="ReadmeLines" />
  </ReadLinesFromFile>

  <PropertyGroup>
    <!-- Объединяем строки README с экранированием переносов строки -->
    <SanitizedDescription>@(ReadmeLines, '\n')</SanitizedDescription>
    <!-- Экранируем кавычки для формата VDF -->
    <SanitizedDescription>$(SanitizedDescription.Replace('"', '\"'))</SanitizedDescription>
    
    <!-- Формируем тело VDF файла -->
    <VdfContent>
"workshopitem"
{
	"appid"		"457140"
       "publishedfileid"  "$(ModId)"
       "contentfolder"    "$(TargetDir.TrimEnd('\'))"
       "previewfile"      "$(ProjectDir)$(PreviewName)"
	"visibility"		"$(Visiblity)"
	"title"		"$(ModTitle)"
	"description"		"$(ModDescription)"
	"changenote"		"Version: $(Version)"
}
    </VdfContent>
  </PropertyGroup>

  <!-- Записываем готовый VDF прямо в папку со скомпилированным модом -->
  <WriteLinesToFile File="$(VdfOutputPath)" Lines="$(VdfContent)" Overwrite="true" Encoding="UTF-8" />
  <Message Importance="high" Text="[MSBuild] Generated workshop_build.vdf at $(VdfOutputPath)" />
</Target>
```

---

## Addition (2026-09-22, в рамках текущей задачи, без новой ветки)

Я тут посмотрел в интернете и вот что нашел

dotnet tool install --global Converter.MarkdownToBBCodeNM.Tool --version 1.0.0.29

# Steam
markdown_to_bbcodesteam -i "**raw markdown**"
markdown_to_bbcodesteam -i "~~raw\r\nmarkdown~~" --disableextended

markdown_to_bbcodesteam -i "/markdown.md";
markdown_to_bbcodesteam -i "/markdown.md" -o "/bbcode.txt";
-i or --input accepts both raw markdown and a file path.
-o or --output accepts a file path. If specified, will write the converted BBCode to the file instead of outputting to the console.
-d or --disableextended will disable newline detection via two spaces and will disable HTML conversion

мы можем это интегрировать в нашу сборку? пусть в папку с модом падает README.txt в формате bbcode и затем оттуда будем копировать текст для публикации. А тулзу добавить в зависимости как уже добавлены остальные наши тулзы в PackageReference

---

## Addition (2026-09-22, в рамках текущей задачи)

3. contentfolder пусть тогда смотрит в папку с ${TargetFolder}
надеюсь эта переменная подцепится
