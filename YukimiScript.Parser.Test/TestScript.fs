﻿module YukimiScript.Parser.Test.TestScript

open YukimiScript.Parser.Test.Utils
open NUnit.Framework


[<Test>]
let testExampleScript () =
    testParseScript """
- extern systemAPI_sleep_begin force  # 在这里定义宿主命令
- extern systemAPI_sleep_end
- extern systemAPI_sleep time=1 
- extern systemAPI_jumpToSection target
- extern name

- macro jumpToSection target
__diagram_link_to target
systemAPI_jumpToSection target

- scene "entrypoint"
@jumpToSection "场景 第一个场景"

- macro wait time=1 force=false
@systemAPI_sleep_begin force    # 这里的内容将会被展开
@systemAPI_sleep time
@systemAPI_sleep_end

- scene "场景 第一个场景"
y:你好~我叫[name]，[wait --time 1 --force]<ani 很高兴认识你！> \
欢迎你来我家里玩~
@wait 3
y:感谢您使用由纪美脚本语言！
@wait

# 以上文字内容编译为
# @__text_begin --character y
# @__text_type --text "你好~我叫"
# @name
# @__text_type --text "，"
# @wait --time 1 --force true
# @__text_pushMark --mark ani
# @__text_type --text "很高兴认识你！"
# @__text_popMark --mark ani
# @__text_end --hasMore true
# @__text_begin
# @__text_type "欢迎你来我家里玩~"
# @__text_end --hasMore false

# @__text_begin --character y
# @__text_type --text "感谢您使用由纪美脚本语言！"
# @__text_end



- scene "场景 第一个场景 的子场景" inherit "场景 第一个场景"
# 这个场景的状态机将会继承于"场景 第一个场景".

"""


[<Test>]
let testExternLinker () =
    testParseScript """
        - extern system.hello arg1 arg2=1
        - macro hello arg1 arg2
        @system.hello arg1 arg2

        - scene "main"
        @hello --arg2 2 --arg1 1
        @system.hello 1
        @system.hello --arg2 2 --arg1 1
    """