# Yukimi Script

为描述视觉小说而设计的领域专用语言。

## 概览

```

@Sprite = newObject

- function Sprite.show 
    @systemAPI.show self

- function createSprite path blend mask tranparent
    @sprite := newObject -metaclass=Sprite
    @sprite.image := loadImage -file=path
    @sprite.blend := blend
    @sprite.mask := loadImage -file=mask
    @if transparent {
        @system.spriteAPI.makeTransparent sprite
    }
    
    @return sprite

- section a
{
@sprite := createSprite "sprite.png" -blend=normal -mask="sprite mask.png" -transparent
@sprite.show
@wait 5
}

@name := "由纪美"
@y := makeCharacter -name=name
@ani := createTextAnimation -type="Jump" -smooth
y:你好~我叫[name]，[wait 1]<ani 很高兴认识你！> \
欢迎你来我家里玩~
y:感谢您使用由纪美脚本语言！

```

* 类似Lua的原型链面向对象。
* 完善的文本描述语法，并将此语法编译到普通的函数调用。
* 类似krkr的键值对传参和flags传参语法。
* 按行Parse，便于分析。
* 支持用于设计编辑器的扩展语法，用于为编辑器提供额外信息。
* 是描述视觉小说的领域专用语言，同时也可以进行通用编程。
* 基于《空梦》的开发经验设计，充分向其他视觉小说开发者学习开发经验，以可用性为优先。
