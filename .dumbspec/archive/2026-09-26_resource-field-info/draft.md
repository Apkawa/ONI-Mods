# 2026-09-26_resource-field-info: draft

## Raw input (verbatim)

Новый мод ResourceFieldInfo - просмотр мощности месторождения
(общий объем бассейна воды или магмы или любого твердного натурального ресурса, или газа, в клетках и в кг). 
Логика - считаем  все непрерывные без разрывов клетки и суммируем вес каждой клетки (он может быть разный).
Очень актуально если есть бассейн магмы и мы хотим оценить сколько там будет тонн породы, или оценить запасы воды. 
Газ тоже считаем, будет полезно для бесконечного хранилища газа, чтобы понять сколько газа утрамбовали уже.

Считается при наведении мышью на материал при нажатом Ctrl, выводится в тултипе. При этом не должно быть активировано никакие инструменты и режима постройки.

Формат примерно такой, например:
```
Клетки: 100
Всего: 100т
```
i18n максимально переиспользуем

```
$ rg -i '"Клетки"'
OxygenNotIncluded_Data/StreamingAssets/strings/strings_preinstalled_ru_klei.po
119207:msgstr "Клетки"

$ rg -i '"Всего"'
OxygenNotIncluded_Data/StreamingAssets/strings/strings_preinstalled_ru_klei.po
95389:msgstr "Всего"
```

## Follow-up instruction (user, refinement step)

> делай автономно

Explicit user instruction to skip all gates: refine, spec review and plan confirmation are not required; run the whole process through execution autonomously.
