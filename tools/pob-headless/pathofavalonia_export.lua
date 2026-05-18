local inputPath = arg[1]
local outputPath = arg[2]

local function jsonEscape(value)
  value = tostring(value or "")
  value = value:gsub("\\", "\\\\")
  value = value:gsub("\"", "\\\"")
  value = value:gsub("\n", "\\n")
  value = value:gsub("\r", "\\r")
  return value
end

local function writeJson(data)
  local out = assert(io.open(outputPath, "w"))
  out:write(data)
  out:close()
end

local function fail(message)
  writeJson("{\"success\":false,\"error\":\"" .. jsonEscape(message) .. "\"}")
end

if not inputPath or not outputPath then
  fail("Usage: pathofavalonia_export.lua <input.xml> <output.json>")
  os.exit(1)
end

local ok, err = pcall(function()
  local input = assert(io.open(inputPath, "r"))
  local xml = input:read("*a")
  input:close()

  package.path = "../runtime/lua/?.lua;../runtime/lua/?/init.lua;?.lua;" .. package.path
  local wrapper = dofile("HeadlessWrapper.lua")
  local loadBuildFromXML = _G.loadBuildFromXML or (type(wrapper) == "table" and wrapper.loadBuildFromXML)
  if type(loadBuildFromXML) ~= "function" then
    error("HeadlessWrapper.lua did not expose loadBuildFromXML")
  end

  local build = loadBuildFromXML(xml, "PathOfAvalonia import")
  for _ = 1, 20 do
    if build and build.calcsTab and type(build.calcsTab.BuildOutput) == "function" then
      build.calcsTab:BuildOutput()
    end
  end

  local mainOutput = build and build.calcsTab and build.calcsTab.mainOutput or {}
  local stats = {}
  local keyStats = {
    "FullDPS", "TotalDPS", "CombinedDPS", "Life", "EnergyShield",
    "Armour", "Evasion", "FireResist", "ColdResist", "LightningResist", "ChaosResist"
  }
  for _, stat in ipairs(keyStats) do
    local value = mainOutput[stat]
    if value ~= nil then
      stats[#stats + 1] = "{\"stat\":\"" .. jsonEscape(stat) .. "\",\"value\":\"" .. jsonEscape(value) .. "\",\"displayValue\":\"" .. jsonEscape(value) .. "\"}"
    end
  end

  local rows = {}
  local skillDps = mainOutput.SkillDPS or {}
  for _, row in pairs(skillDps) do
    if type(row) == "table" then
      local name = row.name or row.Name or row.skillName or row.label or "Skill"
      local dps = row.dps or row.DPS or row.value or row.Value or ""
      local count = row.count or row.Count or 1
      local part = row.skillPart or row.part or row.sourceSkillPart
      local source = row.source or row.Source
      rows[#rows + 1] = "{\"name\":\"" .. jsonEscape(name) .. "\",\"dps\":\"" .. jsonEscape(dps) .. "\",\"displayDps\":\"" .. jsonEscape(dps) .. "\",\"count\":" .. tostring(count) .. ",\"skillPart\":\"" .. jsonEscape(part) .. "\",\"source\":\"" .. jsonEscape(source) .. "\"}"
    end
  end

  local backendName = "PathOfBuilding"
  local cwd = io.popen("pwd")
  if cwd then
    local current = cwd:read("*a") or ""
    cwd:close()
    if current:find("PoE2") then
      backendName = "PathOfBuilding-PoE2"
    end
  end

  writeJson("{\"success\":true,\"backendName\":\"" .. backendName .. "\",\"playerStats\":[" .. table.concat(stats, ",") .. "],\"skillDps\":[" .. table.concat(rows, ",") .. "],\"warnings\":[]}")
end)

if not ok then
  fail(err)
  os.exit(1)
end
