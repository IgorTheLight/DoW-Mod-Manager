del *.exe
PUSHD ..
copy bin\Debug\DoW*.exe LatestStable\
POPD
rename DoW*.exe "DoW Mod Manager.exe"
notepad version