"""Build editable Shader Graphs from this project's Unity 17.3 node schemas.

No generated HLSL/custom function nodes. Preserve graph asset GUIDs.
"""
import copy
import json
import pathlib
import uuid

ROOT = pathlib.Path(__file__).resolve().parents[2]
OUT = ROOT / 'Assets/Environment/CombatRuins/ShaderGraphs'

def read(path):
    text = path.read_text(encoding='utf-8-sig')
    decoder, objects, offset = json.JSONDecoder(), [], 0
    while offset < len(text):
        offset += len(text[offset:]) - len(text[offset:].lstrip())
        if offset == len(text):
            break
        obj, offset = decoder.raw_decode(text, offset)
        objects.append(obj)
    return objects

surface = read(ROOT / 'Assets/Resources/Shader/URPMaskMap.shadergraph')
water = read(ROOT / 'Assets/Resources/Shader/WaterShader.shadergraph')
decal = read(next((ROOT / 'Library/PackageCache').glob('com.unity.shadergraph@*/GraphTemplates/Cross Pipeline/0_Decal Simple.shadergraph')))
pool = surface + water + decal
objects = {x['m_ObjectId']: x for x in pool}

def kind(x):
    return x['m_Type'].split('.')[-1]

class Graph:
    def __init__(self, template, name):
        self.name, self.count = name, 0
        self.root = copy.deepcopy(template[0])
        self.root['m_Path'] = 'Checkmate/Ruins'
        self.items = [self.root]
        for field in ['m_Properties', 'm_CategoryData', 'm_Nodes', 'm_Edges', 'm_GroupDatas', 'm_StickyNoteDatas']:
            self.root[field] = []
        for ctx in ['m_VertexContext', 'm_FragmentContext']:
            self.root[ctx]['m_Blocks'] = []
        self.root['m_FragmentContext']['m_Position'] = {'x': 1900., 'y': 0.}
        self.root['m_VertexContext']['m_Position'] = {'x': 1900., 'y': -350.}
        target = next(x for x in template if kind(x) == 'UniversalTarget')
        subtarget = next(x for x in template if x['m_ObjectId'] == target['m_ActiveSubTarget']['m_Id'])
        self.items += [copy.deepcopy(target), copy.deepcopy(subtarget)]
        self.root['m_ActiveTargets'] = [{'m_Id': target['m_ObjectId']}]
        self.blocks = {}
        for x in template:
            if kind(x) == 'BlockNode':
                self.block(x['m_SerializedDescriptor'], x)

    def uid(self):
        self.count += 1
        return uuid.uuid5(uuid.NAMESPACE_URL, self.name + '/' + str(self.count)).hex

    def node(self, template, x=0, y=0):
        n = copy.deepcopy(template)
        n['m_ObjectId'] = self.uid()
        n['m_Group'] = {'m_Id': ''}
        n['m_DrawState']['m_Position'].update(x=float(x), y=float(y))
        slots = []
        for ref in n['m_Slots']:
            s = copy.deepcopy(objects[ref['m_Id']])
            s['m_ObjectId'] = self.uid()
            self.items.append(s)
            slots.append({'m_Id': s['m_ObjectId']})
        n['m_Slots'] = slots
        self.items.append(n)
        self.root['m_Nodes'].append({'m_Id': n['m_ObjectId']})
        return n

    def slot(self, n, slot):
        return next(x for x in self.items if x['m_ObjectId'] in [s['m_Id'] for s in n['m_Slots']] and x['m_Id'] == slot)

    def value(self, n, slot, value):
        s = self.slot(n, slot)
        if isinstance(s['m_Value'], dict):
            s['m_Value'] = {k: value for k in s['m_Value']}
        else:
            s['m_Value'] = value

    def edge(self, source, destination, output=0, input=0):
        self.root['m_Edges'].append({'m_OutputSlot': {'m_Node': {'m_Id': source['m_ObjectId']}, 'm_SlotId': output}, 'm_InputSlot': {'m_Node': {'m_Id': destination['m_ObjectId']}, 'm_SlotId': input}})

    def block(self, descriptor, template=None):
        template = template or next(x for x in pool if kind(x) == 'BlockNode' and x['m_SerializedDescriptor'] == descriptor)
        n = self.node(template)
        n['m_Name'] = n['m_SerializedDescriptor'] = descriptor
        if descriptor != template['m_SerializedDescriptor']:
            slot = self.slot(n, 0)
            slot['m_DisplayName'] = descriptor.split('.')[-1]
            slot['m_ShaderOutputName'] = descriptor.split('.')[-1]
        context = 'm_VertexContext' if descriptor.startswith('Vertex') else 'm_FragmentContext'
        self.root[context]['m_Blocks'].append({'m_Id': n['m_ObjectId']})
        self.blocks[descriptor.split('.')[-1]] = n
        return n

    def property(self, name, typename, value, x=-1500, y=0):
        p = copy.deepcopy(next(o for o in pool if kind(o) == typename + 'ShaderProperty'))
        p['m_ObjectId'] = self.uid()
        p['m_Guid']['m_GuidSerialized'] = str(uuid.UUID(self.uid()))
        p['m_Name'] = name
        p['m_DefaultReferenceName'] = p['m_OverrideReferenceName'] = '_' + name
        p['m_RefNameGeneratedByDisplayName'] = name
        p['m_Value'] = value
        if typename == 'Vector1':
            p['m_FloatType'] = 0
        if typename == 'Texture2D':
            p['isMainTexture'] = name in ('BaseMap', 'Color')
            p['m_DefaultType'] = 3 if name == 'NormalMap' else (1 if name == 'EmissionMap' else 0)
        if typename == 'Color':
            p['isMainColor'] = name == 'BaseColor'
        self.items.append(p)
        self.root['m_Properties'].append({'m_Id': p['m_ObjectId']})
        prop_nodes = [o for o in pool if kind(o) == 'PropertyNode' and kind(objects[o['m_Property']['m_Id']]) == typename + 'ShaderProperty']
        n = self.node(prop_nodes[0], x, y)
        n['m_Property'] = {'m_Id': p['m_ObjectId']}
        return n

    def scalar(self, name, value, y=0):
        return self.property(name, 'Vector1', value, y=y)

    def color(self, name, rgb, y=0):
        return self.property(name, 'Color', dict(zip(('r', 'g', 'b', 'a'), (*rgb, 1.))), y=y)

    def texture(self, name, y=0, normal=False):
        p = self.property(name, 'Texture2D', {'m_SerializedTexture': '', 'm_Guid': ''}, y=y)
        n = self.node(next(o for o in surface if kind(o) == 'SampleTexture2DNode' and bool(o['m_TextureType']) == normal), -1200, y)
        self.edge(p, n, input=1)
        return n

    def op(self, typename, a=None, b=None, ao=0, bo=0, x=0, y=0):
        n = self.node(next(o for o in pool if kind(o) == typename + 'Node'), x, y)
        if a is not None: self.edge(a, n, output=ao)
        if b is not None: self.edge(b, n, output=bo, input=1)
        return n

    def save(self):
        ids = {o['m_ObjectId']: o for o in self.items}
        assert len(ids) == len(self.items)
        for e in self.root['m_Edges']:
            for side in ('m_OutputSlot', 'm_InputSlot'):
                v = e[side]
                n = ids[v['m_Node']['m_Id']]
                assert any(ids[s['m_Id']]['m_Id'] == v['m_SlotId'] for s in n['m_Slots'])
        path = OUT / (self.name + '.shadergraph')
        path.write_text('\n\n'.join(json.dumps(x, indent=4) for x in self.items) + '\n', encoding='utf-8')
        print(path.name, len(self.root['m_Nodes']), 'nodes;', len(self.root['m_Edges']), 'validated edges')

g = Graph(surface, 'RuinSurface_Master')
base = g.texture('BaseMap', -200)
tint = g.color('BaseColor', (.94, .96, .92), -500)
base = g.op('Multiply', base, tint, x=-500, y=-200)
wet = g.scalar('Wetness', .18, 850)
wet_tint = g.color('WetTint', (.62, .76, .73), 1050)
wet_color = g.op('Lerp', b=wet_tint, x=0, y=-200)
g.value(wet_color, 0, 1.)
g.edge(wet, wet_color, input=2)
final_base = g.op('Multiply', base, wet_color, ao=2, bo=3, x=500, y=-200)
g.edge(final_base, g.blocks['BaseColor'], output=2)
normal = g.texture('NormalMap', 150, True)
strength = g.scalar('NormalStrength', .65, 450)
normal = g.op('NormalStrength', normal, strength, x=-400, y=150)
g.edge(normal, g.blocks['NormalTS'], output=2)
metal = g.texture('MetallicMap', 1450)
metal = g.op('Multiply', metal, g.scalar('Metallic', 1., 1750), ao=4, x=300, y=500)
g.edge(metal, g.blocks['Metallic'], output=2)
rough = g.texture('RoughnessMap', 2050)
smooth = g.op('OneMinus', rough, ao=4, x=-600, y=900)
smooth = g.op('Multiply', smooth, g.scalar('Smoothness', .85, 2450), ao=1, x=-250, y=900)
smooth = g.op('Lerp', smooth, g.scalar('WetSmoothness', .72, 2750), ao=2, x=350, y=900)
g.edge(wet, smooth, input=2)
g.edge(smooth, g.blocks['Smoothness'], output=3)
emission = g.texture('EmissionMap', 3050)
emission = g.op('Multiply', emission, g.color('EmissionColor', (.12, .28, .25), 3450), x=600, y=1450)
g.edge(emission, g.blocks['Emission'], output=2)
g.value(g.blocks['Occlusion'], 0, 1.)
g.save()

for name, is_moss in [('RuinMoss_Decal', True), ('RuinWaterline_Decal', False)]:
    g = Graph(decal, name)
    next(x for x in g.items if kind(x) == 'UniversalDecalSubTarget')['m_DecalData']['affectsMAOS'] = True
    color = g.texture('Color', -450)
    tint = g.color('Tint', (.62, .77, .48) if is_moss else (.18, .29, .26), -700)
    color = g.op('Multiply', color, tint, x=0, y=-450)
    g.edge(color, g.blocks['BaseColor'], output=2)
    mask = g.texture('MaskMap', 0)
    uv = g.node(next(x for x in water if kind(x) == 'UVNode'), -1500, 600)
    split = g.op('Split', uv, x=-1200, y=600)
    inv_u = g.op('OneMinus', split, ao=1, x=-900, y=500)
    inv_v = g.op('OneMinus', split, ao=2, x=-900, y=850)
    eu = g.op('Multiply', split, inv_u, ao=1, bo=1, x=-600, y=500)
    ev = g.op('Multiply', split, inv_v, ao=2, bo=1, x=-600, y=850)
    edge = g.op('Multiply', eu, ev, ao=2, bo=2, x=-300, y=650)
    edge = g.op('Multiply', edge, g.scalar('EdgeFalloff', 64., 1350), ao=2, x=0, y=650)
    edge = g.op('Saturate', edge, ao=2, x=300, y=650)
    alpha = g.op('Multiply', mask, edge, ao=4, bo=1, x=650, y=200)
    if is_moss:
        noise = g.op('GradientNoise', uv, x=-700, y=1200)
        g.value(noise, 1, 5.)
        threshold = g.op('Smoothstep', x=-250, y=1200)
        g.value(threshold, 0, .34)
        g.value(threshold, 1, .64)
        g.edge(noise, threshold, output=2, input=2)
        alpha = g.op('Multiply', alpha, threshold, ao=2, bo=3, x=800, y=650)
    alpha = g.op('Multiply', alpha, g.scalar('Opacity', .7, 1700), ao=2, x=1000, y=200)
    g.edge(alpha, g.blocks['Alpha'], output=2)
    for block, value in [('Metallic', 0.), ('Occlusion', 1.), ('Smoothness', .22 if is_moss else .8)]:
        b = g.block('SurfaceDescription.' + block)
        g.value(b, 0, value)
    g.edge(g.scalar('Smoothness', .22 if is_moss else .8, 2000), g.blocks['Smoothness'])
    maos = g.block('SurfaceDescription.MAOSAlpha', next(x for x in decal if kind(x) == 'BlockNode' and x['m_SerializedDescriptor'] == 'SurfaceDescription.Alpha'))
    g.edge(alpha, maos, output=2)
    g.save()
