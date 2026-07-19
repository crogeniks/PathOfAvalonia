-- Exports Path of Building's timeless-jewel Lua tables and compressed LUTs
-- into the compact assets consumed by PathOfAvalonia.
--
-- Usage:
--   lua tools/export_timeless_jewels.lua \
--     ../PathOfBuilding/src/Data/TimelessJewelData \
--     ../PathOfBuilding/src/TreeData/legion \
--     assets/PoE1/TimelessJewels

local dataDir = arg[1]
local spriteDir = arg[2]
local outputDir = arg[3]
if not dataDir or not spriteDir or not outputDir then
    io.stderr:write("usage: lua export_timeless_jewels.lua <PoB timeless data dir> <PoB legion sprite dir> <output dir>\n")
    os.exit(1)
end

local function join(left, right)
    return left:gsub("/$", "") .. "/" .. right
end

local function mkdir(path)
    assert(os.execute(string.format("mkdir -p %q", path)))
end

local function read(path)
    local file = assert(io.open(path, "rb"))
    local value = file:read("*a")
    file:close()
    return value
end

local function write(path, value)
    local file = assert(io.open(path, "wb"))
    file:write(value)
    file:close()
end

local function copy(source, target)
    write(target, read(source))
end

local function jsonString(value)
    value = value:gsub("\\", "\\\\")
        :gsub('"', '\\"')
        :gsub("\b", "\\b")
        :gsub("\f", "\\f")
        :gsub("\n", "\\n")
        :gsub("\r", "\\r")
        :gsub("\t", "\\t")
    return '"' .. value .. '"'
end

local function sortedKeys(value)
    local keys = { }
    for key in pairs(value) do
        keys[#keys + 1] = key
    end
    table.sort(keys, function(left, right)
        if type(left) == type(right) then
            return left < right
        end
        return tostring(left) < tostring(right)
    end)
    return keys
end

local encode
encode = function(value)
    local kind = type(value)
    if kind == "nil" then return "null" end
    if kind == "boolean" then return value and "true" or "false" end
    if kind == "number" then return string.format("%.15g", value) end
    if kind == "string" then return jsonString(value) end
    if kind ~= "table" then error("cannot JSON encode " .. kind) end

    local count = 0
    local array = true
    for key in pairs(value) do
        count = count + 1
        if type(key) ~= "number" or key < 1 or key % 1 ~= 0 then
            array = false
        end
    end
    if array then
        for index = 1, count do
            if value[index] == nil then
                array = false
                break
            end
        end
    end

    local parts = { }
    if array then
        for index = 1, count do
            parts[index] = encode(value[index])
        end
        return "[" .. table.concat(parts, ",") .. "]"
    end

    for _, key in ipairs(sortedKeys(value)) do
        parts[#parts + 1] = jsonString(tostring(key)) .. ":" .. encode(value[key])
    end
    return "{" .. table.concat(parts, ",") .. "}"
end

local function compactTemplate(node)
    local rolls = { }
    for _, statKey in ipairs(node.sortedStats or { }) do
        local stat = node.stats and node.stats[statKey]
        if stat then
            rolls[#rolls + 1] = {
                key = statKey,
                fmt = stat.fmt,
                index = stat.index,
                min = stat.min,
                max = stat.max,
            }
        end
    end
    return {
        id = node.id,
        name = node.dn,
        icon = node.icon,
        stats = node.sd or { },
        rolls = rolls,
    }
end

mkdir(outputDir)
mkdir(join(outputDir, "sprites"))

local legion = assert(loadfile(join(dataDir, "LegionPassives.lua")))()
local definitions = {
    additionOffset = 96,
    additions = { },
    replacements = { },
}
for index, node in ipairs(legion.additions) do
    definitions.additions[index] = compactTemplate(node)
end
for index, node in ipairs(legion.nodes) do
    definitions.replacements[index] = compactTemplate(node)
end
write(join(outputDir, "definitions.json"), encode(definitions) .. "\n")

local sourceMapping = assert(loadfile(join(dataDir, "NodeIndexMapping.lua")))()
local mapping = {
    size = sourceMapping.size,
    sizeNotable = sourceMapping.sizeNotable,
    nodes = { },
    localIds = { },
}
for nodeId, entry in pairs(sourceMapping) do
    if type(nodeId) == "number" and type(entry) == "table" and entry.index then
        mapping.nodes[tostring(nodeId)] = { index = entry.index, size = entry.size }
    end
end
for jewelType, entries in pairs(sourceMapping.localIdToGlobalId or { }) do
    if type(jewelType) == "number" then
        local converted = { }
        for localId, globalId in pairs(entries) do
            if type(localId) == "number" then
                converted[tostring(localId)] = globalId
            end
        end
        mapping.localIds[tostring(jewelType)] = converted
    end
end
write(join(outputDir, "mapping.json"), encode(mapping) .. "\n")

local lookupFiles = {
    BrutalRestraint = "brutal-restraint.z",
    ElegantHubris = "elegant-hubris.z",
    HeroicTragedy = "heroic-tragedy.z",
    LethalPride = "lethal-pride.z",
    MilitantFaith = "militant-faith.z",
}
for sourceName, outputName in pairs(lookupFiles) do
    copy(join(dataDir, sourceName .. ".zip"), join(outputDir, outputName))
end

local gloriousPath = join(dataDir, "GloriousVanity.zip")
local glorious = io.open(gloriousPath, "rb")
if glorious then
    local value = glorious:read("*a")
    glorious:close()
    write(join(outputDir, "glorious-vanity.z"), value)
else
    local parts = { }
    local index = 0
    while true do
        local path = join(dataDir, "GloriousVanity.zip.part" .. index)
        local file = io.open(path, "rb")
        if not file then break end
        parts[#parts + 1] = file:read("*a")
        file:close()
        index = index + 1
    end
    assert(#parts > 0, "Glorious Vanity LUT was not found")
    write(join(outputDir, "glorious-vanity.z"), table.concat(parts))
end

local sourceSprites = assert(loadfile(join(spriteDir, "tree-legion.lua")))()
local spriteMap = { atlases = { } }
for atlasName, candidates in pairs(sourceSprites) do
    local candidate = candidates[1]
    local maxX, maxY = 0, 0
    for _, rect in pairs(candidate.coords) do
        maxX = math.max(maxX, rect.x + rect.w)
        maxY = math.max(maxY, rect.y + rect.h)
    end
    local prefixedName = "legion" .. atlasName:sub(1, 1):upper() .. atlasName:sub(2)
    spriteMap.atlases[prefixedName] = {
        file = "TimelessJewels/sprites/" .. candidate.filename,
        w = maxX,
        h = maxY,
        coords = candidate.coords,
    }
end
write(join(outputDir, "sprites.json"), encode(spriteMap) .. "\n")

for _, fileName in ipairs({
    "keystone-additional-3.png",
    "keystone-additional-disabled-3.png",
    "skills-additional-3.jpg",
    "skills-additional-disabled-3.jpg",
}) do
    copy(join(spriteDir, fileName), join(join(outputDir, "sprites"), fileName))
end

io.stderr:write(string.format(
    "wrote %d additions, %d replacements, and %d node mappings to %s\n",
    #definitions.additions,
    #definitions.replacements,
    sourceMapping.size,
    outputDir))
