﻿module YukimiScript.Parser.Test.TestScript

open YukimiScript.Parser.Test.Utils
open NUnit.Framework


let private example = """
- import "lib.ykm"
- import "lib2.ykm"

- scene "entrypoint"
@jumpToSection "场景 第一个场景"

- macro wait time=1
@systemAPI_sleep time

- scene "场景 第一个场景"
y:你好~我叫[name]，[wait --time 1]<ani 很高兴认识你！> \
欢迎你来我家里玩~
@wait 3
y:感谢您使用由纪美脚本语言！

# 以上文字内容编译为
# @__text_begin --character y
# @__text_type --text "你好~我叫"
# @name
# @__text_type --text "，"
# @wait --time 1
# @__text_pushMark --mark ani
# @__text_type --text "很高兴认识你！"
# @__text_popMark --mark ani
# @__text_br
# @__text_type "欢迎你来我家里玩~"
# @__text_end

# @__text_begin --character y
# @__text_type --text "感谢您使用由纪美脚本语言！"
# @__text_end



- scene "场景 第一个场景 的子场景" inherit "场景 第一个场景"
# 这个场景的状态机将会继承于"场景 第一个场景".

"""

[<Test>]
let testScript1 () =
    testParseScript example