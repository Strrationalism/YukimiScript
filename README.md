# Yukimi Script

为描述视觉小说而设计的领域专用语言。

## 特点
* 类似Lua的原型链面向对象。
* 完善的文本描述语法，并将此语法编译到普通的函数调用。
* 类似krkr的键值对传参和flags传参语法。
* 按行Parse，便于分析。
* 支持用于设计编辑器的扩展语法，用于为编辑器提供额外信息。
* 是描述视觉小说的领域专用语言，同时也可以进行通用编程。
* 基于《空梦》的开发经验设计，充分向其他视觉小说开发者学习开发经验，以可用性为优先。

## 设计原则
* 易于Parse
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
    - OCaml实现的Native运行时
    - F#实现的.NET运行时
* 本地化差分脚本生成工具
* 配音稿生成工具
* 文档生成工具

## 根据开发经验做出的设计
### 空梦
* 需要单一的声明对象的语法
    - 引入了单独的声明对象的语法
* 需要明确标注对象生存期
    - 引入了作用域语法
* 文本换行语法丑陋
    - 引入了文本换行语法
* 不支持分支跳转
    - 给section命名，允许在section之间跳转
* 不支持跨行字体设置
    - 以类似XML的语法支持设置跨行字体

### 来自krkr的启发
* 引入了看起来像是flags和键值对调用的写法
* 允许在文本间通过方括号嵌入对象以动态生成文本

### 来自BKE的启发
* sprintf
    - 目前不知道这种情况的使用频率，已经支持普通对话文字内嵌表达式，暂不加入
* 广义表语法
    - 通过逗号来创建广义表

### 来自Librian的启发
* 可以同时添加多个标记，可以用表达式生成标记（WIP）
* 可读性极好的立绘语法
    - 可以通过命令加广义表的语法来实现

### 来自NS的启发
* 中缀表达式

### 来自AVGPlus的启发
* Lambda表达式
    - 目前不知道这种情况的使用频率，暂不加入

### 来自Nova的启发
* 通过创建翻译版本差分来进行多语言支持
    - 需要编译器支持，不是语言设计的一部分

### 来自NVLMaker的启发
* 可以支持导出配音文档
    - 需要工具支持，不是语言设计的一部分


## 概览

```
- global                                # 全局可见
@Sprite := new                          # 一切Sprite的原型对象
@Sprite.image := null                   # 定义原型对象中的一些属性
@Sprite.blend := normal                 # 派生对象中找不到属性时，将会从原型对象中搜索
@Sprite.mask := null
@Sprite.transparent := null             # YukimiScript没有false，只有一个flag，使用flag和null分别表示true和false。

# 在第一个section和function开始之前的区域将会在加载当前模块时被执行。

- local                                 # 本代码文件内可见
@ui := createSprite "a"

- scene entrypoint
@jumpToSection a

- method Sprite.show                    # 原型对象中的方法，将会自动传入self参数
@systemAPI.show self

- method createSprite path blend mask=null tranparent=flag  # 一个普通的方法，其self参数是调用此函数的作用域
@sprite := new --class Sprite
@sprite.image := loadImage --file path
@sprite.blend := blend
@sprite.position := 100, 50                   # 这里position使用元组语法，但实际上生成了链表包含成员head和tail
@sprite.mask := loadImage --file mask
@sprite.information := "这个精灵来自于：" + path
@if transparent {
    @sprite.transparent := flag
    @system.spriteAPI.makeTransparent sprite
} else {
    # emmm...
}

@return sprite
    
- method id x
@return x

- scene a                   # 幕a
{                           # 开始一个作用域
@sprite := createSprite "sprite.png" --blend normal --mask "sprite mask.png" --transparent
@sprite.show
@wait 5
}                           # 作用域销毁，引用sprite销毁，其引用计数归0，其对象确定性析构

@name := "由纪美"                                  
@y := makeCharacter --name name  
@ani := createTextAnimation --type "Jump" --smooth
y:你好~我叫[name]，[wait --time 1]<ani, ani2:很高兴认识你！> \
<Font, Font2> {                      # 在这里引入一组可以跨行的字体
欢迎你来我家里玩~
}
y:感谢您使用由纪美脚本语言！

# 以上文字内容编译为
# @_.begin --character y
# @_.text --text "你好~我叫"
# @_.text --text [name]
# @_.text --text "，"
# @_.text [wait --time 1]
# @_.pushMark --mark ani
# @_.pushMark --mark ani2
# @_.text --text "很高兴认识你！"
# @_.popMark --mark ani2
# @_.popMark --mark ani
# @_.br
# @_.pushMark Font
# @_.pushMark Font2
# @_.text "欢迎你来我家里玩~"
# @_.popMark --mark Font2
# @_.popMark --mark Font
# @_.end

# @_.begin --character y
# @_.text --text "感谢您使用由纪美脚本语言！"
# @_.end

# 关于类似[f]这种写法的特殊说明：
# 一个调用表达式内只存在一个符号时执行此特殊的求值方法。
# 搜索此符号时，将会作用域内向外搜索，如果找到一个方法，则会调用它，如果找到一个对象，则会返回该对象自身。
# 如果需要引用该函数自身，可以实现一个id方法，使用id方法来引用函数自身。

# 关于调用时键值对语法的设计：
# 命令放在最左边，之后是使用键值对形式的参数，如果该键值对不存在“值的部分”，则传入true，不存在于参数列表的，则作为null传入。

# 关于成员函数的调用：
# 使用a.b的方式调用时，会向b传入a作为self参数。如果将b单独取出，则需要手动传入self参数。
# 成员函数调用时，Runtime会向其传入-self参数，此传参发生在已经确定符号是函数之后才发生。

# 关于scene：
# 一幕开始时将会重置所有状态机。

```

## 基础对象列表

Yukimi Script必须使用这些对象来实现其基础功能。

### 基础全局方法

| 方法名 | 返回值类型 | 参数类型 | 描述 |
| -----  | --------- | -------- | ---  |
| id     | T         | --x T     | 返回其自身，用于引用一个方法自身。 |
| new | object | --class object/null | 创建一个object，并可以设置一个原型对象。|
| return | bottom | --x T | 使当前函数返回。 |



### 数据类型
* int    - 整数
* number - 有理数
* string - 字符串
* method - 方法
* null   - 空
* flag   - 旗帜
* object - 对象

## 字节码
YukimiScript采用小端序[RIFF格式](https://docs.microsoft.com/zh-cn/windows/win32/multimedia/resource-interchange-file-format-services)进行存储。    
所有区块的大小以4字节对齐。

### RIFF区块
RIFF区块中FormType的字段应该为"YKMO"，包含以下字段：
* META
* SYMS(LIST)
* STRP(LIST)
* PROG(LIST)

### META区块
META区块中包含以下内容：
* version : uint32
    - 表示当前字节码的版本号
* encoding : uint32
    - 此字段表明字符串池所使用的编码：
        + 0 - UTF-8
        + 1 - UCS16-LE

### SYMS区块
SYMS区块为一个LIST区块，其ListType为SYMS，其中包含一些SYMB区块。    
每个SYMB区块包含一个ANSI编码的字符串，不足处补'\0'，表示一个符号。
    
### STRP区块
STRP区块为一个LIST区块，其ListType为STRP，其中包含一些STRC区块。    
每个STRC区块包含一个以META区块中encoding记录的编码方式编码的字符串。

### PROG区块
PROG区块为一个LIST区块，其ListType为PROG，其中包含以下区块：
* METH
* INIT
* SCEN

#### METH区块
METH区块包含了一个方法的字节码。    
TODO：需要讨论它的参数及默认值信息的保存方式。    

#### INIT区块
INIT区块包含了启动时要执行的字节码，INIT区块需要做以下事情：
* 定义源代码中的Global对象
* 将METH区块绑定到各个对象的的方法中

#### SCEN区块
SCEN区块包含了一个场景的代码，其区块前四个字节为一个uint32，为此SCEN的名字。  
可以在STRP区块中找到这个名字的字符串形式。  

