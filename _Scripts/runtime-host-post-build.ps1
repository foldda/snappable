Set-ExecutionPolicy -Scope CurrentUser -ExecutionPolicy Unrestricted
$DebugPreference = "Continue"

$parentPath=(get-item $MyInvocation.MyCommand.Path).Directory.Parent.FullName

Set-Location -Path "$parentPath"

#Write-Output "Sript's parent path [$parentPath]"

$targetPath=$args[0]  #"$parentPath\Manager\bin\Debug"

$HandlerProjects = $args[1]  # "CsvHandler;HL7Handler;MiscHandler;Trigger"   #$args[0]

$ProjectsArray =$HandlerProjects.Split(";")

foreach ($HandlerProject in $ProjectsArray)
{
    # Output the current item
    # Write-Output $HandlerProject

    $sourcePath="$parentPath\$HandlerProject\bin\Debug\netstandard2.0"

    #Write-Output "Copying *.dll *.pdb files from [$sourcePath => $targetPath]"

    ROBOCOPY $sourcePath $targetPath "$HandlerProject.dll" "$HandlerProject.pdb"

}