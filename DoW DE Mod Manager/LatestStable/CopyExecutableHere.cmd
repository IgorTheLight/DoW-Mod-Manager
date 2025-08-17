del *.exe
PUSHD ..
copy bin\Release\DoW*.exe LatestStable\
POPD
rename DoW*.exe "DoW Mod Manager.exe"
notepad version