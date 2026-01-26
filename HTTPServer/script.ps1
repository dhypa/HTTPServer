$path  = "C:\projects\HTTPServer\HTTPServer\message.txt"
$tmp = "C:\projects\HTTPServer\HTTPServer\output.txt"   # change to $in if you want in-place

# Read entire file as text (UTF-8 by default; change if needed)
$text = [System.IO.File]::ReadAllText($path)


# Convert CRLF and CR to LF
$text = $text -replace "`r`n|`r", "`n"


# Write back as UTF-8 (no BOM)
$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllText($tmp, $text, $utf8NoBom)


Move-Item -Force $tmp $path