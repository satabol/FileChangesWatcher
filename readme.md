﻿# Проект для мониторинга за изменениями в файлах.

Мониторинг за изменениями в указанных каталогах и дисках. Последние 5 файлов выводятся в контекстом меню:

![](help/12.png)

## Использование наблюдения за файлами.

Для наблюдения за файлами необходимо указать параметры наблюдения. Параметры наблюдения указываются в файле Stroiproject.ini, который будет автоматически создаваться в каталоге, где находится .exe-файл.

Содержимое файла:

![](help/18.png)

1. Каталоги, за которыми надо наблюдать 
2. Расширения, за которыми надо наблюдать (остальные игнорируются).
3. Каталоги-исключения, содержимое которых которые игнорируется.

Указать каталоги для наблюдения можно и в командной строке:

![](help/13.png)

Для дисков нужно указывать две дроби (не знаю в чём причина). Содержимое параметров командной строки и настроек в ini-файле суммируется, дубликаты каталогов для наблюдения удаляются.

Приложение производит слежение за всеми файлами, находящиеся в указанных директориях, но выводит их через "фильтр", чтобы не перегружать вывод на экран. Список файлов см. в проекте

![](help/14.png)

## Примеры действий с наблюдателем

### Сохранение вложения из outlook

![](help/15.png)

### Сохранения картинки из письма outlook

![](help/16.png)

### Сохранения документа word

![](help/17.png)

## Дополнительные ссылки

Проект чтения файлов иконок для файлов по расширению зарегистрированному в операционной системе: http://www.codeproject.com/Articles/29137/Get-Registered-File-Types-and-Their-Associated-Ico
Эти иконки потом используются в пунктах меню, которые указывают на файлы.
