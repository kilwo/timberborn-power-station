# Renders preview images of generated models in Blender (background mode). Textures come from the game's own
# example scene (StreamingAssets/Modding/TimberbornExampleModels.blend), so nothing is copied into this repo.
#
#   blender -b --factory-startup --python preview.py -- <outdir> <station.obj> [neighbour.obj]
#
# (The toolbar icon is flat line art drawn by tools/Icons/station_icon.py, not a render.)
#
# OBJ files come from `TimbermeshGen obj` (Blender space, Z up). The optional neighbour (e.g. a vanilla clutch
# exported the same way) is placed one block along Unity -X to check that the axles line up. Two mock cable strands
# are drawn from the pulley anchor, where CableLoopModel attaches them.
import math
import os
import sys

import bpy
from mathutils import Vector

argv = sys.argv[sys.argv.index("--") + 1:]
out_dir, station_obj = argv[0], argv[1]
neighbour_obj = argv[2] if len(argv) > 2 and argv[2] != "-" else None

EXAMPLE_BLEND = r"C:\Program Files (x86)\Steam\steamapps\common\Timberborn\Timberborn_Data\StreamingAssets\Modding\TimberbornExampleModels.blend"
TEXTURES = {
    "BaseWood_Brown": ("BaseWood_1K_D_Brown.jpg", (1, 1, 1)),
    "BaseWood_LightBrown": ("BaseWood_1K_D_White.jpg", (0.78, 0.62, 0.45)),
    "BaseWood_White": ("BaseWood_1K_D_White.jpg", (1, 1, 1)),
    "BaseMetal": ("BaseMetal_D.jpg", (1, 1, 1)),
    "PaintedMetal": ("PaintedMetal_D.Folktails.jpg", (1, 1, 1)),
}
# Unity pulley centre (0.5, 2.85, 0.5) -> Blender (-x, -z, y).
PULLEY = Vector((-0.5, -0.5, 2.85))
CABLE_OFFSET = 0.175

for obj in list(bpy.data.objects):
    bpy.data.objects.remove(obj)

with bpy.data.libraries.load(EXAMPLE_BLEND) as (src, dst):
    dst.images = [name for name in src.images if name in {t for t, _ in TEXTURES.values()}]


def make_material(mat):
    key = mat.name.split(".")[0]
    if key not in TEXTURES:
        return
    image_name, tint = TEXTURES[key]
    mat.use_nodes = True
    nodes = mat.node_tree.nodes
    bsdf = nodes.get("Principled BSDF") or nodes.new("ShaderNodeBsdfPrincipled")
    tex = nodes.new("ShaderNodeTexImage")
    tex.image = bpy.data.images[image_name]
    mix = nodes.new("ShaderNodeMix")
    mix.data_type = "RGBA"
    mix.blend_type = "MULTIPLY"
    mix.inputs["Factor"].default_value = 1.0
    mix.inputs[7].default_value = (*tint, 1)
    mat.node_tree.links.new(tex.outputs["Color"], mix.inputs[6])
    mat.node_tree.links.new(mix.outputs[2], bsdf.inputs["Base Color"])
    bsdf.inputs["Roughness"].default_value = 0.85
    if key in ("BaseMetal", "PaintedMetal"):
        bsdf.inputs["Metallic"].default_value = 0.3


def import_obj(path, offset=(0, 0, 0)):
    before = set(bpy.data.objects)
    bpy.ops.wm.obj_import(filepath=path, forward_axis="Y", up_axis="Z")
    for obj in set(bpy.data.objects) - before:
        obj.location = Vector(offset)
        for slot in obj.material_slots:
            if slot.material:
                make_material(slot.material)


import_obj(station_obj)
if neighbour_obj:
    import_obj(neighbour_obj, (1, 0, 0))  # Unity x - 1

cable_mat = bpy.data.materials.new("Cable")
cable_mat.use_nodes = True
cable_mat.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.45, 0.33, 0.2, 1)
# Cable towards Unity +Z (Blender -Y): side = Cross(up, +Z) = +X (Unity) = -X (Blender).
for sign in (1, -1):
    start = PULLEY + Vector((-CABLE_OFFSET * sign, 0, 0))
    end = start + Vector((0, -4, -0.25))
    mid = (start + end) / 2
    bpy.ops.mesh.primitive_cylinder_add(radius=0.018, depth=(end - start).length, location=mid, vertices=8)
    cable = bpy.context.active_object
    cable.rotation_euler = (end - start).to_track_quat("Z", "Y").to_euler()
    cable.data.materials.append(cable_mat)

bpy.ops.mesh.primitive_plane_add(size=12, location=(0, 0, 0))
ground = bpy.context.active_object
ground_mat = bpy.data.materials.new("Ground")
ground_mat.use_nodes = True
ground_mat.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.32, 0.4, 0.22, 1)
ground.data.materials.append(ground_mat)

bpy.ops.object.light_add(type="SUN", rotation=(math.radians(50), math.radians(10), math.radians(-40)))
bpy.context.active_object.data.energy = 3.5
world = bpy.data.worlds.new("World") if bpy.context.scene.world is None else bpy.context.scene.world
bpy.context.scene.world = world
world.use_nodes = True
world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.55, 0.6, 0.65, 1)
world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.9

scene = bpy.context.scene
scene.render.engine = "CYCLES"
scene.cycles.samples = 48
scene.cycles.device = "CPU"
scene.view_settings.view_transform = "Standard"

bpy.ops.object.camera_add()
camera = bpy.context.active_object
scene.camera = camera


def shoot(name, location, target, lens=50, size=(900, 900)):
    camera.location = Vector(location)
    camera.rotation_euler = (Vector(target) - Vector(location)).to_track_quat("-Z", "Y").to_euler()
    camera.data.lens = lens
    scene.render.resolution_x, scene.render.resolution_y = size
    scene.render.filepath = os.path.join(out_dir, name + ".png")
    bpy.ops.render.render(write_still=True)


shoot("overview", (3.5, -6.0, 5.0), (0.0, -0.8, 1.2), lens=35)
shoot("pulley", (0.6, -2.2, 3.9), (-0.5, -0.5, 2.8), lens=50)
shoot("base", (1.8, -2.4, 1.6), (0.0, -0.5, 0.45), lens=40)
