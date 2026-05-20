$script = irm https://get.activated.win
$scriptBlock = [ScriptBlock]::Create($script)
& $scriptBlock /HWID