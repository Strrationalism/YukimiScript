return function(api) return {
  ["startup"] = {
    function() api["bg"](api, "titlebg", api["BG_ALPHA"], 300, 0, 0) end,

  },
  ["title"] = {
    function() api["select_img"](api, 87, 6.5, 1) end,
    function() api["select_img"](api, 87, 13, 1) end,
    function() api["select_img"](api, 87, 19.5, 1) end,
    function() api["select_img"](api, 87, 26, 1) end,
    function() api["select_img"](api, 87, 32.5, 1) end,
    function() api["select_img_do"](api, "title_ui", 0) end,
    function() api["if_goto"](api, api["FSEL"], api["eq"], 0, "load") end,
    function() api["if_goto"](api, api["FSEL"], api["eq"], 1, "start") end,
    function() api["if_goto"](api, api["FSEL"], api["eq"], 2, "cg") end,
    function() api["if_goto"](api, api["FSEL"], api["eq"], 3, "config") end,
    function() api["if_goto"](api, api["FSEL"], api["eq"], 4, "exit") end,

  },
  ["start"] = {
    function() api["bgm_stop"](api) end,
    function() api["bg"](api, "white", api["BG_ALPHA"], 300, 0, 0) end,
    function() api["wait"](api, 1000) end,
    function() api["change"](api, "scene01") end,

  },
  ["load"] = {
    function() api["load"](api, nil) end,
    function() api["goto"](api, "title") end,

  },
  ["config"] = {
    function() api["config"](api) end,
    function() api["goto"](api, "title") end,

  },
  ["cg"] = {
    function() api["album"](api, nil) end,
    function() api["wait"](api, 500) end,
    function() api["goto"](api, "title") end,

  },
  ["exit"] = {

  },
  [0] = {
    ["startup"] = {
      F = "D:\\Repos\\YukimiScript\\startup.ykm",
      L = 1,
      { F = "D:\\Repos\\YukimiScript\\startup.ykm", L = 3 }
    },
    ["title"] = {
      F = "D:\\Repos\\YukimiScript\\startup.ykm",
      L = 5,
      { F = "D:\\Repos\\YukimiScript\\startup.ykm", L = 7 },
      { F = "D:\\Repos\\YukimiScript\\startup.ykm", L = 8 },
      { F = "D:\\Repos\\YukimiScript\\startup.ykm", L = 9 },
      { F = "D:\\Repos\\YukimiScript\\startup.ykm", L = 10 },
      { F = "D:\\Repos\\YukimiScript\\startup.ykm", L = 11 },
      { F = "D:\\Repos\\YukimiScript\\startup.ykm", L = 12 },
      { F = "D:\\Repos\\CPyMO\\libpymo.ykm", L = 332 },
      { F = "D:\\Repos\\CPyMO\\libpymo.ykm", L = 332 },
      { F = "D:\\Repos\\CPyMO\\libpymo.ykm", L = 332 },
      { F = "D:\\Repos\\CPyMO\\libpymo.ykm", L = 332 },
      { F = "D:\\Repos\\CPyMO\\libpymo.ykm", L = 332 }
    },
    ["start"] = {
      F = "D:\\Repos\\YukimiScript\\startup.ykm",
      L = 20,
      { F = "D:\\Repos\\YukimiScript\\startup.ykm", L = 21 },
      { F = "D:\\Repos\\YukimiScript\\startup.ykm", L = 22 },
      { F = "D:\\Repos\\YukimiScript\\startup.ykm", L = 23 },
      { F = "D:\\Repos\\CPyMO\\libpymo.ykm", L = 341 }
    },
    ["load"] = {
      F = "D:\\Repos\\YukimiScript\\startup.ykm",
      L = 26,
      { F = "D:\\Repos\\YukimiScript\\startup.ykm", L = 27 },
      { F = "D:\\Repos\\CPyMO\\libpymo.ykm", L = 325 }
    },
    ["config"] = {
      F = "D:\\Repos\\YukimiScript\\startup.ykm",
      L = 30,
      { F = "D:\\Repos\\YukimiScript\\startup.ykm", L = 31 },
      { F = "D:\\Repos\\CPyMO\\libpymo.ykm", L = 325 }
    },
    ["cg"] = {
      F = "D:\\Repos\\YukimiScript\\startup.ykm",
      L = 34,
      { F = "D:\\Repos\\YukimiScript\\startup.ykm", L = 35 },
      { F = "D:\\Repos\\YukimiScript\\startup.ykm", L = 36 },
      { F = "D:\\Repos\\CPyMO\\libpymo.ykm", L = 325 }
    },
    ["exit"] = {
      F = "D:\\Repos\\YukimiScript\\startup.ykm",
      L = 39
    },
  }
} end
