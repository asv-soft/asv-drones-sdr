@echo off
rem ====== projects ======

set projects=Asv.Drones.Sdr

rem ====== projects ======

rem install tool for update project version by git describe
rem dotnet tool install -g dotnet-setversion

rem copy version to text file, then in variable
git describe --tags --abbrev=4 >version.txt
SET /p VERSION=<version.txt
DEL version.txt
SET VERSION=%VERSION:v=%
rem build all projects
(for %%p in (%projects%) do (
  	echo %%p
  	echo %VERSION%
	dotnet restore ./src/%%p/%%p.csproj
	dotnet build /p:SolutionDir=../;ProductVersion=%VERSION% ./src/%%p/%%p.csproj -c Release
	dotnet publish /p:SolutionDir=../;ProductVersion=%VERSION% ./src/%%p/%%p.csproj -c Release -r linux-arm --self-contained -p:PublishSingleFile=true -f net8.0 -o bin/publish/linux-arm
	echo "%VERSION%">>bin/publish/linux-arm/%%p.version
	dotnet publish /p:SolutionDir=../;ProductVersion=%VERSION% ./src/%%p/%%p.csproj -c Release -r linux-x64 --self-contained -p:PublishSingleFile=true -f net8.0 -o bin/publish/linux-x64
	echo "%VERSION%">>bin/publish/linux-x64/%%p.version
	dotnet publish /p:SolutionDir=../;ProductVersion=%VERSION% ./src/%%p/%%p.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -f net8.0 -o bin/publish/win-x64
	echo "%VERSION%">>bin/publish/win-x64/%%p.version
)) 




