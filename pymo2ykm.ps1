if (($args[0] -eq $null) -or ($args[1] -eq $null)) {
    echo "./pymo2ykm <pymoscript.txt> <yukimiscript.ykm>"
    exit
}

$script_name = 
    [System.IO.Path]::GetFileNameWithoutExtension($args[0]).Trim()

$src = Get-Content $args[0]
$output = $args[1]

$lines = @()
foreach ($line in $src) { $lines += $line }

$parsed_lines = @()

function Fix-Text($x) {
    if ([System.String]::IsNullOrWhitespace($x)) { return "" }
    else {
        return $x.Replace('[', '「').Replace(']', '」').Replace('<', '〈').Replace('>', '〉').Replace("--", "——").Replace("`"", "`“")
    }
}

for ($line_index = 0; $line_index -lt $lines.Length; $line_index += 1) {
    $line = $lines[$line_index].Trim()

    if ($line.StartsWith("#")) {
        $line = $line.Substring(1)
        $comment = $null
        $comment_split = $line.IndexOf(";")
        if ($comment_split -gt 0) {
            $comment = $line.Substring($comment_split).TrimStart(';')
            $line = $line.Substring(0, $comment_split).Trim()
        }


        $cmd_arg_split = $line.IndexOf(" ")
        
        if ($cmd_arg_split -lt 0) { $cmd = $line; $args = @() }
        else {
            $cmd = $line.Substring(0, $cmd_arg_split).Trim()
            $args = $line.Substring($cmd_arg_split).Split(',')
        }

        for ($i = 0; $i -lt $args.Length; $i += 1) {
            $args[$i] = $args[$i].Trim()
        }

        if ($cmd -eq "sel") {
            [int]$sel_count = $args[0]
            
            $hint_pic = $null
            if ($args.Length -ge 2) {
                $hint_pic = $args[1]
            }

            $args = @([string]$args[0])

            $line_index += 1
            
            for ($sel_index = 0; $sel_index -lt $sel_count; $sel_index += 1) {
                $selection = $lines[$line_index].Trim()
                $selection += ";"
                $selection_cut = $selection.IndexOf(";")
                $args += $selection.Substring(0, $selection_cut).Trim()
                $line_index += 1
            }

            if ($hint_pic -ne $null) { $args += $hint_pic }
        }

        $parsed_lines += @{ Cmd = $cmd; Args = $args; Comment = $comment; LineNumber = $line_index }
    }
    elseif ($line -ne "") {
        $parsed_lines += @{ Cmd = $null; Args = $null; Comment = $line.TrimStart(';'); LineNumber = $line_index }
    }
    else {
        $parsed_lines += @{ Cmd = $null; Args = $null; Comment = $null; LineNumber = $line_index }
    }
}

$target_text = @("- scene `"`$init`"")
$characters = @{}
$character_count = 0

enum LabelState {
    NoLabel
    HasLabel
    HasLabelSameNameWithScriptAndItsFirstLabel
}

[LabelState]$label_state = [LabelState]::NoLabel
[string []]$labels = @()

function Str($arg) { return "`"$arg`"" }

foreach ($cmd in $parsed_lines) {
    if ($cmd.Cmd -eq "say") {
        if ($cmd.Args.Length -eq 2) {
            $character_name = Fix-Text($cmd.Args[0])
            if (!$characters.Contains($character_name)) {
                $character_symbol = "c" + $character_count
                $character_count += 1
                $ykm_define_stat = "@__define_character "
                $ykm_define_stat += $character_symbol
                $ykm_define_stat += " "
                $ykm_define_stat += Str($character_name)
                $target_text += $ykm_define_stat
                $characters[$character_name] = $character_symbol
            }
        }
    }

    elseif ($cmd.Cmd -eq "label") {
        $labels += $cmd.Args[0]

        if ($label_state -eq [LabelState]::NoLabel) {
            if ($cmd.Args[0] -eq $script_name) {
                $label_state = [LabelState]::HasLabelSameNameWithScriptAndItsFirstLabel
            }
            else {
                $label_state = [LabelState]::HasLabel
            }
        }
    }
}

function Rename-MainScene($ls, $script_name) {
    while ($true) {
        if ($ls.Contains($script_name)) {
            $script_name = "OLD_" + $script_name
            continue
        }

        return $script_name
    }
}

$target_text += ""
$rename_main_scene_to = $null

switch ($label_state) {
    NoLabel {
        $target_text += "- scene `"$script_name`""
        $rename_main_scene_to = $script_name
        break
    }

    HasLabel {
        $rename_main_scene_to = Rename-MainScene $labels $script_name
        $target_text += "- scene `"$script_name`""
        $first = $labels[0]
        $target_text += "@goto `"$first`""
        break
    }

    HasLabelSameNameWithScriptAndItsFirstLabel {
        $rename_main_scene_to = $script_name
        break
    }
}

function Color($arg) {
    return $arg.Replace("#", "0x")
}

function Bool($arg) {
    if ($arg -eq "0") { return "false" }
    else { return "true" }
}

function Gen-Ykm($pymo) {
    $ret = @()

    if ($pymo.Cmd -eq $null) {
        if ($pymo.Comment -ne $null) {
            return @("# $($pymo.Comment)")
        }
        else { return @("") }
    }

    switch ($pymo.Cmd) {
        # 1. 文本
        "say" {
            $stat = ""
            $text = $pymo.Args[0]

            if ($pymo.Args.Length -eq 2) {
                $ch = Fix-Text($pymo.Args[0])
                $stat += $characters[$ch]
                $stat += ":"
                $text = $pymo.Args[1]
            }

            $text = Fix-Text($text)

            if (($text -eq "") -or ($text -eq $null)) { $text = @() }
            else { $text = $text.Split("\n") }

            $text_tmp = @()
            foreach ($t in $text) {
                $t = $t.Trim()
                if (![System.String]::IsNullOrWhitespace($t)) {
                    $text_tmp += $t
                }
            }

            $text = $text_tmp

            for ($text_index = 0; $text_index -lt $text.Length; $text_index += 1) {
                $stat += $text[$text_index]
                if ($text_index -ne $text.Length - 1) {
                    $stat += "\"
                }

                $ret += $stat
                
                $stat = ""
            }

            break
        }

        "text" {
            $content = Str($pymo.Args[0])
            $x1 = $pymo.Args[1]
            $y1 = $pymo.Args[2]
            $x2 = $pymo.Args[3]
            $y2 = $pymo.Args[4]
            $color = Color($pymo.Args[5])
            $size = $pymo.Args[6]
            $imm = Bool($pymo.Args[7])
            $ret += "@text $content $x1 $y1 $x2 $y2 $color $size $imm"
            break
        }

        "text_off" { $ret += "@text_off"; break }
        "waitkey" { $ret += "@waitkey"; break }
        "title" { $ret += "@title $(Str($pymo.Args[0]))"; break }
        "title_dsp" { $ret += "@title_dsp"; break }


        # 2. 图形

        "chara" {
            if ($pymo.Args.Length -eq 5) {
                $pymo.Args[1] = Str($pymo.Args[1])
                $ret += "@chara $($pymo.Args)"
            } else {
                for ($i = 0; $i -lt $pymo.Args.Length; $i += 4) {
                    if ($i + 4 -gt $pymo.Args.Length) { break }
                    
                    $cid = $pymo.Args[$i]
                    $f = Str($pymo.Args[$i + 1])
                    $pos = $pymo.Args[$i + 2]
                    $layer = $pymo.Args[$i + 3]
                    $ret += "@chara_multi $cid $f $pos $layer"
                }

                $ret += "@chara_multi_do --time $($pymo.Args[$pymo.Args.Length - 1])"
            }

            break
        }

        "chara_cls" {
            if ($pymo.Args.Length -ge 2) {
                $ret += "@chara_cls $($pymo.Args[0]) --time $($pymo.Args[1])"
            } else {
                $ret += "@chara_cls $($pymo.Args[0])"
            }
            
            break
        }

        "chara_pos" {
            $cid = pymo.Args[0]
            $x = $pymo.Args[1]
            $y = $pymo.Args[2]
            $cm = "cm$($pymo.Args[3])"
            $ret += "@chara_pos $cid $x $y $cm"
            break
        }

        "bg" {
            if ($pymo.Args.Length -ge 2) {
                $pymo.Args[1] = $pymo.Args[1].ToUpper()
                $t = $pymo.Args[1]
                if (($t -ne "BG_ALPHA") -and ($t -ne "BG_FADE") -and ($t -ne "BG_NOFADE")) {
                    $pymo.Args[1] = Str($pymo.Args[1])
                }
            }

            $pymo.Args[0] = Str($pymo.Args[0])

            $ret += "@bg $($pymo.Args)"
            break
        }

        "flash" { $ret += "@flash $(Color($pymo.Args[0])) $($pymo.Args[1])"; break }
        "quake" { $ret += "@quake"; break }
        "fade_out" { $ret += "@fade_out $(Color($pymo.Args[0])) $($pymo.Args[1])"; break }
        "fade_in" { $ret += "@fade_in $(Color($pymo.Args[0]))"; break }
        "movie" { $ret += "@movie $(Str($pymo.Args[0]))"; break }
        "textbox" { $ret += "@textbox $(Str($pymo.Args[0])) $(Str($pymo.Args[1]))"; break }

        "chara_quake" {
            if ($pymo.Args.Length -eq 1) {
                $ret += "@chara_quake $($pymo.Args[0])"
            } else {
                foreach ($a in $pymo.Args) {
                    $ret += "@chara_quake_multi $a"
                }
                
                $ret += "@chara_quake_multi_do"
            }

            break
        }

        "chara_down" {
            if ($pymo.Args.Length -eq 1) {
                $ret += "@chara_down $($pymo.Args[0])"
            } else {
                foreach ($a in $pymo.Args) {
                    $ret += "@chara_down_multi $a"
                }

                $ret += "@chara_down_multi_do"
            }

            break
        }

        "chara_up" {
            if ($pymo.Args.Length -eq 1) {
                $ret += "@chara_up $($pymo.Args[0])"
            } else {
                foreach ($a in $pymo.Args) {
                    $ret += "@chara_up_multi $a"
                }

                $ret += "@chara_up_multi_do"
            }

            break
        }

        "scroll" {
            $filename = Str($pymo.Args[0])
            $x = $pymo.Args[1]
            $y = $pymo.Args[2]
            $ex = $pymo.Args[3]
            $ey = $pymo.Args[4]
            $time = $pymo.Args[5]
            $ret += "@scroll $filename $x $y $ex $ey $time"
            break
        }

        "chara_y" {
            $cm = "cm$($pymo.Args[0])"

            if ($pymo.Args.Length -eq 7) {
                $cid = $pymo.Args[1]
                $f = Str($pymo.Args[2])
                $x = $pymo.Args[3]
                $y = $pymo.Args[4]
                $layer = $pymo.Args[5]
                $time = $pymo.Args[6]
                $ret += "@chara_y $cm $cid $f $x $y $layer $time"
            } else {
                $i = 0
                while ($i + 5 -lt $pymo.Args.Length) {
                    $cid = $pymo.Args[$i + 1]
                    $f = Str($pymo.Args[$i + 2])
                    $x = $pymo.Args[$i + 3]
                    $y = $pymo.Args[$i + 4]
                    $layer = $pymo.Args[$i + 5]

                    $ret += "@chara_y_multi $cid $f $x $y $layer"

                    $i += 5
                }

                $ret += "@chara_y_multi_do $cm $($pymo.Args[$pymo.Args.Length - 1])"
            }

            break
        }

        "chara_scroll" {
            $cm = "cm$($pymo.Args[0])"
            $cid = $pymo.Args[1]
            if ($pymo.Args.Length -eq 10) {
                $filename = Str($pymo.Args[2])
                $sx = $pymo.Args[3]
                $sy = $pymo.Args[4]
                $ex = $pymo.Args[5]
                $ey = $pymo.Args[6]
                $ba = $pymo.Args[7]
                $layer = $pymo.Args[8]
                $time = $pymo.Args[9]
                $ret += "@chara_scroll_complex $cm $cid $filename $sx $sy $ex $ey $ba $layer $time"
            } else {
                $ex = $pymo.Args[2]
                $ey = $pymo.Args[3]
                $t = $pymo.Args[4]

                $ret += "@chara_scroll $cm $cid $ex $ey $t"
            }
            break
        }

        "anime_on" { 
            $num = $pymo.Args[0]
            $filename = Str($pymo.Args[1])
            $x = $pymo.Args[2]
            $y = $pymo.Args[3]
            $inverval = $pymo.Args[4]
            $isloop = $pymo.Args[5]
            $ret += "@anime_on $num $filename $x $y $interval $isloop"
            break
        }

        "anime_off" { $ret += "@anime_off $(Str($pymo.Args[0]))"; break }

        "chara_anime" {
            $cid = $pymo.Args[0]
            $period = $pymo.Args[1]
            $loop_num = $pymo.Args[2]
            
            for ($i = 3; $i -lt $pymo.Args.Length; $i += 2) {
                $ret += "@chara_anime $($pymo.Args[$i]) $($pymo.Args[$i + 1])"
            }

            $ret += "@chara_anime_do $cid $period $loop_num"
            break
        }

        # 3. 逻辑

        
        "set" { $ret += "@set $($pymo.Args[0]) $($pymo.Args[1])"; break}
        "add" { $ret += "@add $($pymo.Args[0]) $($pymo.Args[1])"; break}
        "sub" { $ret += "@sub $($pymo.Args[0]) $($pymo.Args[1])"; break}

        "label" {
            $label_name = $pymo.Args[0]
            $ret += "@goto `"$label_name`""
            $ret += ""
            $ret += "- scene `"$label_name`""

            break
        }

        "goto" { $ret += "@goto " + (Str $pymo.Args[0]); break }

        "if" {
            $ops = @{ 
                "==" = "eq"; 
                "!=" = "ne";
                "<>" = "ne";
                ">"  = "gt";
                ">=" = "ge";
                "<"  = "lt";
                "<=" = "le";
                "="  = "eq" 
            }

            $op = $null
            $op_index = -1

            foreach ($opi in $ops.Keys) {
                $op_index = $pymo.Args[0].IndexOf($opi)
                if ($op_index -ge 0) {
                    $op = $opi
                    break
                }
            }

            $left = $pymo.Args[0].Substring(0, $op_index)
            $right = $pymo.Args[0].Substring($op_index + $op.Length)

            $scene = $pymo.Args[1].Trim().Substring(5).Trim()
            
            $ret += "@if_goto $left $($ops[$op]) $right $(Str($scene))"

            break
        }

        "change" { $ret += "@change $(Str($pymo.Args[0]))"; break }
        "call" { $ret += "@call $(Str($pymo.Args[0]))"; break }
        "ret" { $ret += "@ret"; break }

        "sel" {
            $selection_count = [int]$pymo.Args[0]
            foreach ($i in 0..($selection_count - 1)) {
                $ret += "@sel $(Str($pymo.Args[1 + $i]))"
            }

            $hint_pic_index = $selection_count + 1

            if ($pymo.Args.Length -gt $hint_pic_index) {
                $ret += "@sel_do $($pymo.Args[$hint_pic_index])"
            } else {
                $ret += "@sel_do"
            }

            break
        }

        "select_text" {
            $selection_count = [int]$pymo.Args[0]
            foreach ($i in 0..($selection_count - 1)) {
                $ret += "@select_text $(Str($pymo.Args[1 + $i]))"
            }

            $x1 = $pymo.Args[1 + $selection_count]
            $y1 = $pymo.Args[2 + $selection_count]
            $x2 = $pymo.Args[3 + $selection_count]
            $y2 = $pymo.Args[4 + $selection_count]
            $col = Color($pymo.Args[5 + $selection_count])
            $init_pos = $pymo.Args[6 + $selection_count]

            $ret += "@select_text_do $x1 $y1 $x2 $y2 --color $col --init_position $init_pos"
            break
        }

        
        "select_var" {
            [int]$choices = $pymo.Args[0]
            
            for ($i = 0; $i -lt $choices; $i += 1) {
                $ret += "@select_var $(Str($pymo.Args[$i * 2 + 1])) $($pymo.Args[$i * 2 + 2])"
            }

            $x = $pymo.Args[1 + $choices * 2]
            $y = $pymo.Args[1 + $choices * 2 + 1]
            $x2 = $pymo.Args[1 + $choices * 2 + 2]
            $y2 = $pymo.Args[1 + $choices * 2 + 3]
            $col = Color($pymo.Args[1 + $choices * 2 + 4])
            $init = $pymo.Args[1 + $choices * 2 + 5]

            $ret += "@select_var_do $x $y $x2 $y2 $col $init"
            break
        }

        "select_img" {
            [int]$choices = $pymo.Args[0]
            $filename = Str($pymo.Args[1])
            
            for ($i = 0; $i -lt $choices; $i += 1) {
                $x = $($pymo.Args[$i * 3 + 2])
                $y = $($pymo.Args[$i * 3 + 3])
                $v = $($pymo.Args[$i * 3 + 4])
                $ret += "@select_img $x $y $v"
            }

            $init = $pymo.Args[2 + $choices * 3]

            $ret += "@select_img_do $filename $init"
            break
        }
        
        "select_imgs" {
            [int]$choices = $pymo.Args[0]
            
            for ($i = 0; $i -lt $choices; $i += 1) {
                $filename = Str($pymo.Args[$i * 4 + 1])
                $x = $($pymo.Args[$i * 4 + 2])
                $y = $($pymo.Args[$i * 4 + 3])
                $v = $($pymo.Args[$i * 4 + 4])
                $ret += "@select_imgs $filename $x $y $v"
            }

            $init = $pymo.Args[1 + $choices * 4]

            $ret += "@select_imgs_do $init"
            break
        }
        
        "wait" { 
            if ($pymo.Args.Length -eq 0) {
                $ret += "@wait 300"
            }
            else {
                $ret += "@wait $($pymo.Args[0])"
            }
            break 
        }

        "wait_se" { $ret += "@wait_se"; break }
        "rand" { $ret += "@rand $($pymo.Args[0]) $($pymo.Args[1]) $($pymo.Args[2])"; break }


        # 4. 声音


        "bgm" {
            if ($pymo.Args.Length -ge 2) {
                $ret += "@bgm $(Str($pymo.Args[0]))"
            } else {
                $ret += "@bgm $(Str($pymo.Args[0])) --isloop $(Bool($pymo.Args[1]))"
            }

            break
        }

        "bgm_stop" { $ret += "@bgm_stop"; break }

        "se" {
            if ($pymo.Args.Length -eq 1) {
                $ret += "@se $(Str($pymo.Args[0]))"
            } else {
                $ret += "@se $(Str($pymo.Args[0])) --isloop $(Bool($pymo.Args[1]))"
            }

            break
        }

        "se_stop" { $ret += "@se_stop"; break }
        "vo" { $ret += "@vo $(Str($pymo.Args[0]))"; break }

        # 5. 系统

        "load" {
            if ($pymo.Args.Length -ge 1) {
                $ret += "@load --save_num $($pymo.Args[0])"
            } else {
                $ret += "@load"
            }

            break
        }

        "album" {
            if ($pymo.Args.Length -ge 1) {
                $ret += "@album --album_list_filename $(Str($pymo.Args[0]))"
            } else {
                $ret += "@album"
            }

            break
        }

        "music" { $ret += "@music"; break }

        "date" {
            $date_bg = $(Str($pymo.Args[0]))
            $x = $($pymo.Args[1])
            $y = $($pymo.Args[2])
            $col = $(Color($pymo.Args[3]))
            $ret += "@date $date_bg $x $y $col"
            break
        }

        "config" { $ret += "@config"; break }


        default {
            Write-Host "Warning: In line $($pymo.LineNumber), $($pymo.Cmd) is not supported."
            $ret += "#$($pymo.Cmd) $($pymo.Args)"
            break
        }
    }

    if ($pymo.Comment -ne $null) {
        $ret[0] = $ret[0].PadRight(16) + "# " + $pymo.Comment
    }

    return $ret
}

foreach ($pymo in $parsed_lines) {
    if (($pymo.Cmd -eq "label") -or ($pymo.Cmd -eq "goto")) {
        if ($pymo.Args[0] -eq $script_name) {
            $pymo.Args[0] = $rename_main_scene_to
        }
    }

    if ($pymo.Cmd -eq "if") {
        if ($pymo.Args[1].Contains($script_name)) {
            $pymo.Args[1] = 
                $pymo.Args[1].Replace($script_name, $rename_main_scene_to)
        }
        
    }

    $target_text += Gen-Ykm($pymo)
}

$target_text | Out-File -FilePath $output -Encoding utf8

