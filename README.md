# Yukimi Script

为描述视觉小说而设计的领域专用语言。

参见[Github Wiki页面](https://github.com/Strrationalism/YukimiScript/wiki)。

## 适用于
* 视觉小说
* 文字类游戏
* 需要对话演出的游戏

## 特点
* 类似krkr的键值对传参和flags传参语法。
* 按行Parse，便于分析。
* 以可用性为优先。
* 可选的静态强类型系统。
* 可编译到多个目标。

## 谁在使用？

[![弦语蝶梦游戏工作室 - 《空梦》](https://cdn.cloudflare.steamstatic.com/steam/apps/1059850/header_schinese.jpg?t=1629427718)](https://store.steampowered.com/app/1059850/)

## 设计原则
* 按行Parse
* 易于实现实时可视化编辑器
* 可以实时检查任意点状态
* 引入的特性需要切实解决实际开发中遇到的问题

## 安装

你可以在[这里](https://marketplace.visualstudio.com/items?itemName=seng-jik.yukimiscript-syntax-highlight)安装用于Visual Studio Code的YukimiScript代码高亮工具。

### 在.NET中通过NuGet安装YukimiScript命令行工具

```shell
dotnet tool install -g YukimiScript.CommandLineTool
```

### 手动安装不依赖.NET运行时的YukimiScript命令行工具

在[Release页面](https://github.com/Strrationalism/YukimiScript/releases)下载对应平台的可执行文件并将其拷贝到可被命令行环境访问的目录中。

### 在.NET项目中引用YukimiScript Parser

```shell
dotnet add package YukimiScript.Parser
```


## 概览

```
- extern systemAPI_sleep_begin force  # 在这里定义宿主命令
- extern systemAPI_sleep_end
- extern systemAPI_sleep time=1 
- extern systemAPI_jumpToSection target
- extern name

- macro jumpToSection target
@__diagram_link_to target
@systemAPI_jumpToSection target

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
# @__text_begin --character "y"
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

# @__text_begin --character "y"
# @__text_type --text "感谢您使用由纪美脚本语言！"
# @__text_end


- scene "场景 第一个场景 的子场景" inherit "场景 第一个场景"
# 这个场景的状态机将会继承于"场景 第一个场景".

```
