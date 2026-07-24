<#
	.DESCRIPTION
	Deregistriert eine TypeLib aus der Registry
#>
Function UnregisterTypeLib($classId)
{
	$regkeypath = "HKLM:\SOFTWARE\Classes\TypeLib\" + $classId
	if (Test-Path $regkeypath) 
	{
		
		$versionKeys = Get-ChildItem $regkeypath |
            ForEach-Object { Get-ItemProperty $_.pspath } |
            ForEach-Object { $_.PSChildName + "\" + $_."(default)"}

		Write-Host "Unregistering TypeLib $classId ($($versionKeys -join ','))"
        Remove-Item $regkeypath -Recurse
	}
}

Function UnregisterCLSID($classId)
{
	$regkeypath = "HKLM:\SOFTWARE\Classes\CLSID\" + $classId
	if (Test-Path $regkeypath) 
	{
		
		$versionKeys = Get-ChildItem $regkeypath |
            ForEach-Object { Get-ItemProperty $_.pspath } |
            ForEach-Object { $_.PSChildName + "\" + $_."(default)"}

		Write-Host "Unregistering CLSID $classId ($($versionKeys -join ','))"
        Remove-Item $regkeypath -Recurse
	}
	
	$regkeypath = "HKLM:\SOFTWARE\Classes\Wow6432Node\CLSID\" + $classId
	if (Test-Path $regkeypath) 
	{
		
		$versionKeys = Get-ChildItem $regkeypath |
            ForEach-Object { Get-ItemProperty $_.pspath } |
            ForEach-Object { $_.PSChildName + "\" + $_."(default)"}

		Write-Host "Unregistering CLSID $classId ($($versionKeys -join ','))"
        Remove-Item $regkeypath -Recurse
	}
}


# Unregister CP_Common
UnregisterTypeLib "{A0DCD767-2262-4228-9B52-A0642C9D6A75}"

# Unregister CP_Contracts
UnregisterTypeLib "{C5CE8735-5F4D-4C52-A999-6CBEEC5A2587}"

# Unregister ImportTest
UnregisterTypeLib "{81A684C4-2D06-4796-B9FB-368241C91EA2}"
UnregisterCLSID "{81A684C4-2D06-4796-B9FB-368241C91EA2}"
