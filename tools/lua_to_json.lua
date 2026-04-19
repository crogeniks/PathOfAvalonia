-- Converts PoB tree.lua to a slim JSON for the Avalonia port.
-- Keeps only the fields the PoC needs: node id/position/flags/edges,
-- group x/y/orbits, and orbit constants.

local inPath = arg[1]
local outPath = arg[2]
if not inPath or not outPath then
    io.stderr:write("usage: lua lua_to_json.lua <tree.lua> <out.json>\n")
    os.exit(1)
end

local chunk, err = loadfile(inPath)
if not chunk then error(err) end
local tree = chunk()

-- tiny JSON encoder (no external deps)
local enc
local function encString(s)
    s = s:gsub('\\', '\\\\'):gsub('"', '\\"'):gsub('\n', '\\n'):gsub('\r', '\\r'):gsub('\t', '\\t')
    return '"' .. s .. '"'
end

local function isArray(t)
    local n = 0
    for k, _ in pairs(t) do
        if type(k) ~= 'number' then return false end
        n = n + 1
    end
    for i = 1, n do
        if t[i] == nil then return false end
    end
    return true, n
end

enc = function(v)
    local t = type(v)
    if t == 'nil' then return 'null' end
    if t == 'boolean' then return v and 'true' or 'false' end
    if t == 'number' then
        if v ~= v then return 'null' end -- NaN
        if v == math.huge or v == -math.huge then return 'null' end
        if v % 1 == 0 and math.abs(v) < 1e15 then return string.format('%d', v) end
        return string.format('%.10g', v)
    end
    if t == 'string' then return encString(v) end
    if t == 'table' then
        local ok, n = isArray(v)
        if ok then
            local parts = {}
            for i = 1, n do parts[i] = enc(v[i]) end
            return '[' .. table.concat(parts, ',') .. ']'
        else
            local parts = {}
            for k, val in pairs(v) do
                parts[#parts + 1] = encString(tostring(k)) .. ':' .. enc(val)
            end
            return '{' .. table.concat(parts, ',') .. '}'
        end
    end
    error('cannot encode ' .. t)
end

-- Slim the groups
local groups = {}
for gid, g in pairs(tree.groups or {}) do
    groups[tostring(gid)] = {
        x = g.x,
        y = g.y,
        orbits = g.orbits or {},
    }
end

-- Slim the nodes
local function asIntList(t)
    if not t then return {} end
    local out = {}
    for i, v in ipairs(t) do
        local n = tonumber(v)
        if n then out[#out + 1] = n end
    end
    return out
end

local nodes = {}
for key, n in pairs(tree.nodes or {}) do
    if key ~= 'root' then
        local id = tonumber(key) or tonumber(n.skill)
        if id then
            nodes[tostring(id)] = {
                id = id,
                name = n.name,
                group = n.group,
                orbit = n.orbit,
                orbitIndex = n.orbitIndex,
                ["out"] = asIntList(n["out"]),
                ["in"] = asIntList(n["in"]),
                isNotable = n.isNotable or false,
                isKeystone = n.isKeystone or false,
                isMastery = n.isMastery or false,
                isJewelSocket = n.isJewelSocket or false,
                isProxy = n.isProxy or false,
                ascendancyName = n.ascendancyName,
                classStartIndex = n.classStartIndex,
                isAscendancyStart = n.isAscendancyStart or false,
            }
        end
    end
end

local slim = {
    min_x = tree.min_x,
    min_y = tree.min_y,
    max_x = tree.max_x,
    max_y = tree.max_y,
    constants = {
        skillsPerOrbit = tree.constants.skillsPerOrbit,
        orbitRadii = tree.constants.orbitRadii,
    },
    groups = groups,
    nodes = nodes,
}

local f = assert(io.open(outPath, 'w'))
f:write(enc(slim))
f:close()

local nCount, gCount = 0, 0
for _ in pairs(nodes) do nCount = nCount + 1 end
for _ in pairs(groups) do gCount = gCount + 1 end
io.stderr:write(string.format('wrote %s : %d nodes, %d groups\n', outPath, nCount, gCount))
