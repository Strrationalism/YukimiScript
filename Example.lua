function Example(api) return {
  ["第一章"] = {
    -- 这里是第一章的内容

    -- 这是一个编译器宏，用于指示流程图生成器表示当前scene可以连接到target scene
    function() api:__system_jump("第二章 开始", true) end,    -- 这里执行系统API，用于进行真实的跳转操作

  },
  ["第二章 开始"] = {
    -- 这是一个编译器宏，用于指示流程图生成器表示当前scene可以连接到target scene
    function() api:__system_jump("第二章 分支A", false) end,    -- 这里执行系统API，用于进行真实的跳转操作

    -- 这是一个编译器宏，用于指示流程图生成器表示当前scene可以连接到target scene
    function() api:__system_jump("第二章 分支B", false) end,    -- 这里执行系统API，用于进行真实的跳转操作


  },
  ["第二章 分支A"] = {
    -- 这是一个编译器宏，用于指示流程图生成器表示当前scene可以连接到target scene
    function() api:__system_jump("第二章 结束", false) end,    -- 这里执行系统API，用于进行真实的跳转操作


  },
  ["第二章 分支B"] = {
    -- 这是一个编译器宏，用于指示流程图生成器表示当前scene可以连接到target scene
    function() api:__system_jump("第二章 结束", false) end,    -- 这里执行系统API，用于进行真实的跳转操作


    -- inherit用于指示当前scene的状态机继承于哪个场景
    -- 仅在需要继承上一个场景的状态时使用，用于提示可视化编辑器使用某个继承来的状态
    -- 注意在跳转API中需要指明不重置状态机

  },
  ["第二章 结束"] = {

    -- 如果有多个inherit，那么使用第一个即可。
    -- 因为inherit仅用于提示编辑器继承于哪个状态机，只需保证每个分支状态一致即可。
    -- inherit不参与流程图生成，流程图生成仅参考__diagram_link_to宏在何处
  },
  ["main"] = {
    -- 这里可以用来定义UI之类的东西

    -- 最后可以执行一步jump，以跳跃到剧本开始的位置上
    -- 剧本相关的内容应当放入scenario目录，只有scenario目录下的内容才参与流程图生成
    -- 这是一个编译器宏，用于指示流程图生成器表示当前scene可以连接到target scene
    function() api:__system_jump("第一章", true) end,    -- 这里执行系统API，用于进行真实的跳转操作


  },
} end
