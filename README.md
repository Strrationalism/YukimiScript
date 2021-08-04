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

## 概览

```

@Sprite := newObject                    # 一切Sprite的原型对象
@Sprite.image := null                   # 定义原型对象中的一些属性
@Sprite.blend := normal                 # 派生对象中找不到属性时，将会从原型对象中搜索
@Sprite.mask := null
@Sprite.transparent := null             # YukimiScript没有false，只有一个true，使用null表示false。

- method Sprite.show                    # 原型对象中的方法，将会自动传入self参数
    @systemAPI.show self

- method createSprite path blend mask tranparent  # 一个普通的方法，其self参数是调用此函数的作用域
    @sprite := newObject -metaclass=Sprite
    @sprite.image := loadImage -file=path
    @sprite.blend := blend
    @sprite.mask := loadImage -file=mask
    @if transparent {
        @sprite.transparent := true
        @system.spriteAPI.makeTransparent sprite
    }
    
    @return sprite
    
- method id x
    @return x

- section a                 # 剧情段a
{                           # 开始一个作用域
@sprite := createSprite "sprite.png" -blend=normal -mask="sprite mask.png" -transparent
@sprite.show
@wait 5
}                           # 作用域销毁，引用sprite销毁，其引用计数归0，其对象确定性析构

@name := "由纪美"                                  
@y := makeCharacter -name=name  
@ani := createTextAnimation -type="Jump" -smooth
y:你好~我叫[name]，[wait -time 1]<ani 很高兴认识你！> \
欢迎你来我家里玩~
y:感谢您使用由纪美脚本语言！

# 以上文字内容编译为
# @_.begin -character=y
# @_.text -text "你好~我叫"
# @_.text -text [name]
# @_.text -text "，"
# @_.text -text [wait -time 1]
# @_.pushBlock -mark ani
# @_.text -text "很高兴认识你！"
# @_.popBlock
# @_.br
# @_.text "欢迎你来我家里玩~"
# @_.end

# @_.begin -character=y
# @_.text "感谢您使用由纪美脚本语言！"
# @_.end

# 关于类似[f]这种写法的特殊说明：
# 仅在一对方括号内只存在一个符号时执行此特殊的求值方法。
# 搜索此符号时，将会作用域内向外搜索，如果找到一个方法，则会调用它，如果找到一个对象，则会返回该对象自身。
# 如果需要引用该函数自身，可以实现一个id方法，使用id方法来引用函数自身。

# 关于调用时键值对语法的设计：
# 命令放在最左边，之后是使用键值对形式的参数，如果该键值对不存在“值的部分”，则传入true，不存在于参数列表的，则作为null传入。

```

## 基础对象列表

Yukimi Script必须使用这些对象来实现其基础功能。

### 基础全局方法

| 方法名 | 返回值类型 | 参数类型 | 描述 |
| -----  | --------- | -------- | ---  |
| id     | T         | -x=T     | 返回其自身，用于引用一个方法自身。 |
| newObject | object | -metaclass=object/null | 创建一个object，并可以设置一个metaclass。|


### 基础全局对象
* Int    - 整数
* Number - 有理数
* String - 字符串
* Method - 方法
* null   - 空
* true   - 真
