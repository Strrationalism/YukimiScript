# Yukimi Script

为描述视觉小说而设计的领域专用语言。

## 特点
* 类似krkr的键值对传参和flags传参语法。
* 按行Parse，便于分析。
* 基于《空梦》的开发经验设计，充分向其他视觉小说开发者学习开发经验，以可用性为优先。

## 设计原则
* 按行Parse
* 易于实现实时可视化编辑器
* 可以实时检查任意点状态
* 引入的特性需要切实解决实际开发中遇到的问题

## YukimiScript Command Line Tool
```
Usage: ykmc <path-to-scripts> [options]

Options:
    -lib <libDir>         Add other library.
    -dgml <output>        Create the diagram.

Examples:
    Check the scripts:
        ykmc "./scripts" -lib "./api"
    Create the diagram from scripts:
        ykmc "./scripts" -lib "./api" -dgml "./diagram.dgml"

```


## 概览

```
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

```

## 内置宏

内置宏是由语言预定义的宏，内置宏由两个__开头，当用户重定义内置宏时，将会优先按照用户的定义进行展开。    
宏可以由语言元素转换为内置宏，比如用于描述文本的__text族宏，也可以用作手动使用以向编译器提供更多信息，如__diagram宏。    
如果用户自行重定义了用于向编译器提示信息的宏，则其内置定义会失效，但如果该宏为编译器向用户提供信息，则用户可以重定义该内置宏以进行回调。    

| 宏名称        | 参数           | 从何处展开            | 展开为           | 描述         |
| ------------ | ------------- | ------------------- |---------------- | ----------- |
| __diagram_link_to | target        | 用户定义的场景跳转API  | 消除              | 用于向流程图生成器指示当前scene可以跳转到哪些scene |
| __text_begin | character=null| 文本语法             | 不展开            | 用于描述“开始一组文本”并指定当前讲话的角色 |
| __text_type  | text          | 文本语法             | 不展开            | 用于描述“输出一段文本”，它之前一定会有__text_begin |
| __text_pushMark | mark       | 文本标记语法          | 不展开            | 用于描述“开始一段被mark对象标记的文本”            |
| __text_popMark  | mark       | 文本标记语法          | 不展开            | 用于描述“取消上一个标记”，同时将会指定要取消哪个标记，会严格按照出栈顺序进行弹出 |
| __text_end   | hasMore=false | 文本语法             | 不展开            | 表示一组文本已经结束，如果末尾有换行符，则hasMore将会为true提示当前一组还有更多文本 |
