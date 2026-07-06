let SessionLoad = 1
let s:so_save = &g:so | let s:siso_save = &g:siso | setg so=0 siso=0 | setl so=-1 siso=-1
let v:this_session=expand("<sfile>:p")
doautoall SessionLoadPre
silent only
silent tabonly
cd ~/Documents/CodeModernizer
if expand('%') == '' && !&modified && line('$') <= 1 && getline(1) == ''
  let s:wipebuf = bufnr('%')
endif
let s:shortmess_save = &shortmess
set shortmess+=aoO
badd +15 ~/Documents/CodeModernizer/src/CodeModernizer.Api/Contracts.cs
badd +2 ~/Documents/CodeModernizer/output:extension-output-asvetliakov.vscode-neovim-\%231-vscode-neovim\%20messages
badd +199 ~/Documents/CodeModernizer/src/CodeModernizer.Api/Program.cs
badd +1 ~/Documents/CodeModernizer/src/CodeModernizer.Core/Models/AiModels.cs
badd +1 ~/Documents/CodeModernizer/src/CodeModernizer.Core/Abstractions/IAiProvider.cs
badd +1 ~/Documents/CodeModernizer/src/CodeModernizer.Core/Abstractions/IDiffService.cs
badd +1 ~/Documents/CodeModernizer/src/CodeModernizer.Core/Abstractions/ISessionStore.cs
badd +1 ~/Documents/CodeModernizer/src/CodeModernizer.Core/Abstractions/ISkillRegistry.cs
badd +44 ~/Documents/CodeModernizer/src/CodeModernizer.Infrastructure/Diff/DiffService.cs
badd +1 ~/Documents/CodeModernizer/src/CodeModernizer.Infrastructure/Providers/AiProviderRegistry.cs
badd +101 ~/Documents/CodeModernizer/src/CodeModernizer.Infrastructure/Providers/ClaudeProvider.cs
badd +92 ~/.dotnet/symbolcache/341e43244547bbada279c50a90387a85d602012df9025019de57c5292fd913b5/RawContentBlockDelta.cs
badd +26 ~/Documents/CodeModernizer/skills/java-21/prompt.md
badd +1 ~/Documents/CodeModernizer/src/CodeModernizer.Api/appsettings.json
badd +310 ~/Documents/CodeModernizer/src/CodeModernizer.Infrastructure/Services/ModernizationService.cs
badd +10 ~/Documents/CodeModernizer/src/CodeModernizer.Infrastructure/Services/CodeResponseParser.cs
argglobal
%argdel
wincmd t
let s:save_winminheight = &winminheight
let s:save_winminwidth = &winminwidth
set winminheight=0
set winheight=1
set winminwidth=0
set winwidth=1
tabnext 1
if exists('s:wipebuf') && len(win_findbuf(s:wipebuf)) == 0 && getbufvar(s:wipebuf, '&buftype') isnot# 'terminal'
  silent exe 'bwipe ' . s:wipebuf
endif
unlet! s:wipebuf
set winheight=1 winwidth=20
let &shortmess = s:shortmess_save
let &winminheight = s:save_winminheight
let &winminwidth = s:save_winminwidth
let s:sx = expand("<sfile>:p:r")."x.vim"
if filereadable(s:sx)
  exe "source " . fnameescape(s:sx)
endif
let &g:so = s:so_save | let &g:siso = s:siso_save
set hlsearch
let g:this_session = v:this_session
let g:this_obsession = v:this_session
doautoall SessionLoadPost
unlet SessionLoad
" vim: set ft=vim :
