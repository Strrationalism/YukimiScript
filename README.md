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

## 工具链
* 编译器
    - 目标为json表示的AST
    - 目标为RIFF表示的字节码
* Uni-Gal互编译器
    - Uni-Gal到Yukimi Script的编译器
    - Yukimi Script到Uni-Gal编译器
    - Uni-Gal API集
* 运行时
    - Rust实现的Native运行时
* 本地化差分脚本生成工具
* 配音稿生成工具

## 根据开发经验做出的设计
### 空梦
* 文本换行语法丑陋
    - 引入了文本换行语法
* 不支持分支跳转
    - 给scene命名，允许在scene之间跳转
* 需要子过程
    - 可以定义macro

### 来自krkr的启发
* 引入了看起来像是flags和键值对调用的写法
* 允许在文本间通过方括号嵌入对象以动态生成文本
    
### 来自Nova的启发
* 通过创建翻译版本差分来进行多语言支持
    - 需要编译器支持，不是语言设计的一部分

### 来自NVLMaker的启发
* 可以支持导出配音文档
    - 需要工具支持，不是语言设计的一部分


## 概览

```
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

```


