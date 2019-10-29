#[CmdletBinding()]
#Lädt das TFS in diese Datei ;)
function LoadTfs()
{
    [void][System.Reflection.Assembly]::LoadWithPartialName("Microsoft.TeamFoundation.Client")  
    [void][System.Reflection.Assembly]::LoadWithPartialName("Microsoft.TeamFoundation.Build.Client")  
    [void][System.Reflection.Assembly]::LoadWithPartialName("Microsoft.TeamFoundation.Build.Common")      
    [void][System.Reflection.Assembly]::LoadWithPartialName("Microsoft.TeamFoundation.Build.Workflow")          
    [void][System.Reflection.Assembly]::LoadWithPartialName("Microsoft.TeamFoundation.VersionControl.Client")

    # TFS PowerShell-Erweiterungen laden (PowerTools müssen mit PowerShell-Extensions installiert sein)
    if ( (Get-PSSnapin -Name Microsoft.TeamFoundation.PowerShell -ErrorAction SilentlyContinue) -eq $null )
    {
        Add-PSSnapin Microsoft.TeamFoundation.PowerShell
    }
}

#Lädt die SMO Libs für den Zugriff auf den SQL Server
function LoadSmo()
{
   [Reflection.Assembly]::LoadWithPartialName("Microsoft.SqlServer.Smo")  | Out-Null
    [Reflection.Assembly]::LoadWithPartialName("Microsoft.SqlServer.SqlEnum") | Out-Null
    [Reflection.Assembly]::LoadWithPartialName("Microsoft.SqlServer.ConnectionInfo") | Out-Null


    if ( Get-PSSnapin -Registered | where {$_.name -eq 'SqlServerCmdletSnapin100'} )
    {
	    if( !(Get-PSSnapin | where {$_.name -eq 'SqlServerCmdletSnapin100'}))
	    { 
		    Add-PSSnapin SqlServerCmdletSnapin100 | Out-Null
	    } ;
    }
    else
    {
	    if( !(Get-Module | where {$_.name -eq 'sqlps'}))
	    {     
            Push-Location
		    Import-Module 'sqlps' -DisableNameChecking ;
            Pop-Location
	    }  ;
    }    
}

LoadSmo 
LoadTfs

