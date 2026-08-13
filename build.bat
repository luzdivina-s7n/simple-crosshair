@echo off
set CSC=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe
"%CSC%" /nologo /optimize+ /target:winexe /out:simple-crosshair.exe /resource:App.ico,Crosshair.App.ico /r:System.Windows.Forms.dll /r:System.Drawing.dll Crosshair.cs
echo Done.