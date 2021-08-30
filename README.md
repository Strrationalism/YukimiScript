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
* 运行时
* 配音稿生成工具
* 本地化差分脚本生成工具
* DGML剧情线路图工具

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
- scene "entrypoint"
@jumpToSection "场景 第一个场景"

- macro wait time=1
@systemAPI_sleep time

- scene "场景 第一个场景"
y:你好~我叫[name]，[wait --time 1 --force]<ani 很高兴认识你！> \
欢迎你来我家里玩~
@wait 3
y:感谢您使用由纪美脚本语言！

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


## 字节码

字节码以小端序按照[RIFF格式](https://docs.microsoft.com/zh-cn/windows/win32/directshow/avi-riff-file-reference)进行存储。

### RIFF区块
RIFF区块中，fileType为"YKMO"，包含以下子区块：
* meta
* LIST symb
* LIST str_
* LIST scen

### meta区块
meta区块包含以下内容：
* bytecodeVersion: uint32    
* encoding: uint32
* signatrue: uint32[8]

其中bytecodeVersion指定了字节码版本，当前版本为0。    
encoding指定了字符串池中字符串的编码方式：
* 0 - UTF8
* 1 - UCS16-LE

signature的部分则是将除meta区块外所有区块按照先后顺序排列到一起后的字节进行SHA256摘要得到的结果，如果使用了数字签名，则此处存放数字签名。

### LIST symb区块
此区块为一个LIST区块，其formType为"symb"，包含数个symb区块，每个symb区块保存了一串ANSI编码的符号表，按四字节对齐，不足之处补0，末尾至少包含一个0。  
其中第一个symb区块的编号为0，第二个symb区块的编号为1，以此类推。

### LIST strp区块
此区块为一个LIST区块，其formType为"str_"，包含数个str_区块，每个区块保存了一串以指定编码方式编码的字符串，按四字节对齐，不足之处补0，末尾至少包含一个0。

### LIST scen区块
此区块为一个LIST区块，其formType为"scen"，包含数个scen区块，每个区块保存了一个源代码中定义的scene。

其中第一个uint32是当前scene名称在LIST strp区块中的编号，随后跟随一系列操作指令，对于每个指令，它都有如下形式：

```
type CommandCall = {
    callee: uint32            // 被调用命令的名称在LIST symb中的编号
    unnamedArgs: uint32       // 未命名的参数数量
    namedArgs: uint32         // 命名的参数数量
};
```

每个指令后面跟随unnamedArgs个未命名参数，如下：

```
type UnnamedArgs = {
    argType: uint32          // 指示了当前参数的类型
    arg: argType             // 参数
}
```

| argType的值 | 大小（字节） | 类型（在C语言中） | 描述 |
| ---------- | ---------- | -------------- | -------------------- |
|          0 |          4 | int32_t        | 一个整数              |
|          1 |          8 | double         | 一个浮点数             |
|          2 |          4 | uint32_t       | 一个字符串，为此字符串在字符串池中的编号 |
|          3 |          4 | uint32_t       | 一个symbol，为此symbol在符号表中的编号|


在未命名参数后面跟随namedArgs个命名参数，如下：
```
type NamedArgs = {
    name: uint32            // 指示了当前参数名字在字符串池中的编号
    arg: UnnamedArgs
}
```

