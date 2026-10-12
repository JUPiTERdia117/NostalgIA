#!/usr/bin/env python3
import math
import os

OUTPUT_DIR = "Assets/Art/Models"
os.makedirs(OUTPUT_DIR, exist_ok=True)

class ObjBuilder:
    def __init__(self, name):
        self.name = name
        self.vertices = []
        self.normals = []
        self.uvs = []
        self.faces = [] # list of [(v_idx, vt_idx, vn_idx)]

    def add_vertex(self, x, y, z):
        self.vertices.append((x, y, z))
        return len(self.vertices)

    def add_normal(self, nx, ny, nz):
        l = math.sqrt(nx*nx + ny*ny + nz*nz) or 1.0
        self.normals.append((nx/l, ny/l, nz/l))
        return len(self.normals)

    def add_uv(self, u, v):
        self.uvs.append((u, v))
        return len(self.uvs)

    def add_face(self, v_indices, vt_indices=None, vn_indices=None):
        if vt_indices is None:
            vt_indices = [1] * len(v_indices)
        if vn_indices is None:
            vn_indices = [1] * len(v_indices)
        self.faces.append(list(zip(v_indices, vt_indices, vn_indices)))

    def add_quad(self, v1, v2, v3, v4, uv1=(0,0), uv2=(1,0), uv3=(1,1), uv4=(0,1)):
        # calculate normal
        p1 = self.vertices[v1-1]
        p2 = self.vertices[v2-1]
        p3 = self.vertices[v3-1]
        ux, uy, uz = p2[0]-p1[0], p2[1]-p1[1], p2[2]-p1[2]
        vx, vy, vz = p3[0]-p1[0], p3[1]-p1[1], p3[2]-p1[2]
        nx = uy*vz - uz*vy
        ny = uz*vx - ux*vz
        nz = ux*vy - uy*vx
        n_idx = self.add_normal(nx, ny, nz)
        u1_idx = self.add_uv(*uv1)
        u2_idx = self.add_uv(*uv2)
        u3_idx = self.add_uv(*uv3)
        u4_idx = self.add_uv(*uv4)
        self.add_face([v1, v2, v3, v4], [u1_idx, u2_idx, u3_idx, u4_idx], [n_idx, n_idx, n_idx, n_idx])

    def add_tri(self, v1, v2, v3, uv1=(0,0), uv2=(1,0), uv3=(0.5,1)):
        p1 = self.vertices[v1-1]
        p2 = self.vertices[v2-1]
        p3 = self.vertices[v3-1]
        ux, uy, uz = p2[0]-p1[0], p2[1]-p1[1], p2[2]-p1[2]
        vx, vy, vz = p3[0]-p1[0], p3[1]-p1[1], p3[2]-p1[2]
        nx = uy*vz - uz*vy
        ny = uz*vx - ux*vz
        nz = ux*vy - uy*vx
        n_idx = self.add_normal(nx, ny, nz)
        u1_idx = self.add_uv(*uv1)
        u2_idx = self.add_uv(*uv2)
        u3_idx = self.add_uv(*uv3)
        self.add_face([v1, v2, v3], [u1_idx, u2_idx, u3_idx], [n_idx, n_idx, n_idx])

    def save(self):
        filepath = os.path.join(OUTPUT_DIR, f"{self.name}.obj")
        with open(filepath, "w") as f:
            f.write(f"# Object: {self.name}\n")
            f.write(f"o {self.name}\n")
            for v in self.vertices:
                f.write(f"v {v[0]:.4f} {v[1]:.4f} {v[2]:.4f}\n")
            for vt in self.uvs:
                f.write(f"vt {vt[0]:.4f} {vt[1]:.4f}\n")
            for vn in self.normals:
                f.write(f"vn {vn[0]:.4f} {vn[1]:.4f} {vn[2]:.4f}\n")
            for face in self.faces:
                f.write("f " + " ".join(f"{v}/{vt}/{vn}" for v, vt, vn in face) + "\n")
        print(f"Generated {filepath} ({len(self.vertices)} verts, {len(self.faces)} faces)")

# 1. Beveled Hexagonal Platform
def build_hex_platform():
    obj = ObjBuilder("Platform_SanctuaryHex")
    R_top = 5.0
    R_bevel = 4.8
    R_bottom = 4.2
    y_top = 0.5
    y_bevel = 0.35
    y_bottom = -0.5

    # Center top
    c_top = obj.add_vertex(0, y_top, 0)
    c_bot = obj.add_vertex(0, y_bottom, 0)

    top_ring = []
    bev_ring = []
    bot_ring = []

    for i in range(6):
        ang = i * (math.pi / 3.0)
        top_ring.append(obj.add_vertex(R_bevel * math.cos(ang), y_top, R_bevel * math.sin(ang)))
        bev_ring.append(obj.add_vertex(R_top * math.cos(ang), y_bevel, R_top * math.sin(ang)))
        bot_ring.append(obj.add_vertex(R_bottom * math.cos(ang), y_bottom, R_bottom * math.sin(ang)))

    for i in range(6):
        nxt = (i + 1) % 6
        # Top inner fan
        obj.add_tri(c_top, top_ring[i], top_ring[nxt])
        # Top bevel quad
        obj.add_quad(top_ring[i], bev_ring[i], bev_ring[nxt], top_ring[nxt])
        # Side wall quad
        obj.add_quad(bev_ring[i], bot_ring[i], bot_ring[nxt], bev_ring[nxt])
        # Bottom fan
        obj.add_tri(c_bot, bot_ring[nxt], bot_ring[i])

    obj.save()

# 2. Beveled Pathway Stone
def build_pathway_stone():
    obj = ObjBuilder("Platform_PathwayStone")
    L, W, H = 3.5, 2.5, 0.8
    bev = 0.25

    # 16 vertices for beveled box
    # Top face
    t1 = obj.add_vertex(-L+bev, H, -W+bev)
    t2 = obj.add_vertex(L-bev, H, -W+bev)
    t3 = obj.add_vertex(L-bev, H, W-bev)
    t4 = obj.add_vertex(-L+bev, H, W-bev)
    obj.add_quad(t1, t2, t3, t4)

    # Upper rim
    r1 = obj.add_vertex(-L, H-bev, -W)
    r2 = obj.add_vertex(L, H-bev, -W)
    r3 = obj.add_vertex(L, H-bev, W)
    r4 = obj.add_vertex(-L, H-bev, W)

    obj.add_quad(t1, r1, r2, t2)
    obj.add_quad(t2, r2, r3, t3)
    obj.add_quad(t3, r3, r4, t4)
    obj.add_quad(t4, r4, r1, t1)

    # Bottom rim (slight taper)
    b1 = obj.add_vertex(-L*0.9, -H, -W*0.9)
    b2 = obj.add_vertex(L*0.9, -H, -W*0.9)
    b3 = obj.add_vertex(L*0.9, -H, W*0.9)
    b4 = obj.add_vertex(-L*0.9, -H, W*0.9)

    obj.add_quad(r1, b1, b2, r2)
    obj.add_quad(r2, b2, b3, r3)
    obj.add_quad(r3, b3, b4, r4)
    obj.add_quad(r4, b4, b1, r1)

    # Bottom face
    obj.add_quad(b4, b3, b2, b1)

    obj.save()

# 3. Round Stepping Disc
def build_round_stepping():
    obj = ObjBuilder("Platform_RoundStepping")
    segments = 12
    R_top = 1.6
    R_bev = 1.8
    R_bot = 1.3
    y_top = 0.3
    y_bev = 0.15
    y_bot = -0.3

    c_top = obj.add_vertex(0, y_top, 0)
    c_bot = obj.add_vertex(0, y_bot, 0)

    top_ring = []
    bev_ring = []
    bot_ring = []

    for i in range(segments):
        ang = i * (2.0 * math.pi / segments)
        top_ring.append(obj.add_vertex(R_top * math.cos(ang), y_top, R_top * math.sin(ang)))
        bev_ring.append(obj.add_vertex(R_bev * math.cos(ang), y_bev, R_bev * math.sin(ang)))
        bot_ring.append(obj.add_vertex(R_bot * math.cos(ang), y_bot, R_bot * math.sin(ang)))

    for i in range(segments):
        nxt = (i + 1) % segments
        obj.add_tri(c_top, top_ring[i], top_ring[nxt])
        obj.add_quad(top_ring[i], bev_ring[i], bev_ring[nxt], top_ring[nxt])
        obj.add_quad(bev_ring[i], bot_ring[i], bot_ring[nxt], bev_ring[nxt])
        obj.add_tri(c_bot, bot_ring[nxt], bot_ring[i])

    obj.save()

# 4. Celestial Obelisk / Pillar
def build_obelisk():
    obj = ObjBuilder("Pillar_CelestialObelisk")
    base_w = 0.8
    top_w = 0.5
    h_base = 0.4
    h_shaft = 4.5
    h_cap = 5.2

    # Base block
    b1 = obj.add_vertex(-base_w, 0, -base_w)
    b2 = obj.add_vertex(base_w, 0, -base_w)
    b3 = obj.add_vertex(base_w, 0, base_w)
    b4 = obj.add_vertex(-base_w, 0, base_w)

    b5 = obj.add_vertex(-base_w, h_base, -base_w)
    b6 = obj.add_vertex(base_w, h_base, -base_w)
    b7 = obj.add_vertex(base_w, h_base, base_w)
    b8 = obj.add_vertex(-base_w, h_base, base_w)

    obj.add_quad(b4, b3, b2, b1) # bottom
    obj.add_quad(b1, b2, b6, b5)
    obj.add_quad(b2, b3, b7, b6)
    obj.add_quad(b3, b4, b8, b7)
    obj.add_quad(b4, b1, b5, b8)

    # Shaft
    s1 = obj.add_vertex(-top_w, h_shaft, -top_w)
    s2 = obj.add_vertex(top_w, h_shaft, -top_w)
    s3 = obj.add_vertex(top_w, h_shaft, top_w)
    s4 = obj.add_vertex(-top_w, h_shaft, top_w)

    obj.add_quad(b5, b6, s2, s1)
    obj.add_quad(b6, b7, s3, s2)
    obj.add_quad(b7, b8, s4, s3)
    obj.add_quad(b8, b5, s1, s4)

    # Pyramidion apex
    apex = obj.add_vertex(0, h_cap, 0)
    obj.add_tri(s1, s2, apex)
    obj.add_tri(s2, s3, apex)
    obj.add_tri(s3, s4, apex)
    obj.add_tri(s4, s1, apex)

    obj.save()

# 5. Grapple Anchor Orb (Double Ring + Faceted Crystal)
def build_grapple_anchor():
    obj = ObjBuilder("Grapple_AnchorSphere")

    # Center crystal (Octahedron)
    c_top = obj.add_vertex(0, 0.7, 0)
    c_bot = obj.add_vertex(0, -0.7, 0)
    m1 = obj.add_vertex(0.5, 0, 0)
    m2 = obj.add_vertex(0, 0, 0.5)
    m3 = obj.add_vertex(-0.5, 0, 0)
    m4 = obj.add_vertex(0, 0, -0.5)

    obj.add_tri(c_top, m1, m2)
    obj.add_tri(c_top, m2, m3)
    obj.add_tri(c_top, m3, m4)
    obj.add_tri(c_top, m4, m1)
    obj.add_tri(c_bot, m2, m1)
    obj.add_tri(c_bot, m3, m2)
    obj.add_tri(c_bot, m4, m3)
    obj.add_tri(c_bot, m1, m4)

    # Gimbal Ring 1 (XY plane)
    R_ring = 1.1
    thick = 0.12
    segs = 12
    r1_in = []
    r1_out = []
    for i in range(segs):
        ang = i * (2.0 * math.pi / segs)
        r1_in.append(obj.add_vertex((R_ring - thick)*math.cos(ang), (R_ring - thick)*math.sin(ang), 0))
        r1_out.append(obj.add_vertex((R_ring + thick)*math.cos(ang), (R_ring + thick)*math.sin(ang), 0))

    for i in range(segs):
        nxt = (i + 1) % segs
        obj.add_quad(r1_in[i], r1_out[i], r1_out[nxt], r1_in[nxt])
        # Back face
        obj.add_quad(r1_in[nxt], r1_out[nxt], r1_out[i], r1_in[i])

    # Gimbal Ring 2 (YZ plane)
    r2_in = []
    r2_out = []
    R_ring2 = 1.3
    for i in range(segs):
        ang = i * (2.0 * math.pi / segs)
        r2_in.append(obj.add_vertex(0, (R_ring2 - thick)*math.sin(ang), (R_ring2 - thick)*math.cos(ang)))
        r2_out.append(obj.add_vertex(0, (R_ring2 + thick)*math.sin(ang), (R_ring2 + thick)*math.cos(ang)))

    for i in range(segs):
        nxt = (i + 1) % segs
        obj.add_quad(r2_in[i], r2_out[i], r2_out[nxt], r2_in[nxt])
        obj.add_quad(r2_in[nxt], r2_out[nxt], r2_out[i], r2_in[i])

    obj.save()

# 6. Rotating Plasma Saw Hazard
def build_plasma_saw():
    obj = ObjBuilder("Hazard_PlasmaSaw")
    # Central Hub
    hub_r = 0.5
    hub_h = 0.3
    c1 = obj.add_vertex(0, hub_h, 0)
    c2 = obj.add_vertex(0, -hub_h, 0)
    h_top = []
    h_bot = []
    for i in range(8):
        ang = i * (2.0 * math.pi / 8)
        h_top.append(obj.add_vertex(hub_r * math.cos(ang), hub_h, hub_r * math.sin(ang)))
        h_bot.append(obj.add_vertex(hub_r * math.cos(ang), -hub_h, hub_r * math.sin(ang)))

    for i in range(8):
        nxt = (i + 1) % 8
        obj.add_tri(c1, h_top[i], h_top[nxt])
        obj.add_quad(h_top[i], h_bot[i], h_bot[nxt], h_top[nxt])
        obj.add_tri(c2, h_bot[nxt], h_bot[i])

    # 4 Curved Blades
    blade_len = 2.2
    for b in range(4):
        ang_base = b * (math.pi / 2.0)
        ang_tip = ang_base + 0.35
        # blade root
        v_r1 = obj.add_vertex(hub_r * math.cos(ang_base), 0.05, hub_r * math.sin(ang_base))
        v_r2 = obj.add_vertex(hub_r * math.cos(ang_base + 0.25), 0.05, hub_r * math.sin(ang_base + 0.25))
        # blade tip
        v_tip = obj.add_vertex(blade_len * math.cos(ang_tip), 0.0, blade_len * math.sin(ang_tip))
        # bottom root
        v_br1 = obj.add_vertex(hub_r * math.cos(ang_base), -0.05, hub_r * math.sin(ang_base))
        v_br2 = obj.add_vertex(hub_r * math.cos(ang_base + 0.25), -0.05, hub_r * math.sin(ang_base + 0.25))

        # Top & Bottom faces
        obj.add_tri(v_r1, v_r2, v_tip)
        obj.add_tri(v_br2, v_br1, v_tip)
        # Edges
        obj.add_quad(v_r1, v_tip, v_tip, v_br1)
        obj.add_quad(v_r2, v_br2, v_tip, v_tip)

    obj.save()

# 7. Spike Trap Cluster
def build_spike_trap():
    obj = ObjBuilder("Hazard_SpikeTrap")
    # Base slab (2x2)
    s = 1.0
    h_slab = 0.15
    b1 = obj.add_vertex(-s, 0, -s)
    b2 = obj.add_vertex(s, 0, -s)
    b3 = obj.add_vertex(s, 0, s)
    b4 = obj.add_vertex(-s, 0, s)

    t1 = obj.add_vertex(-s, h_slab, -s)
    t2 = obj.add_vertex(s, h_slab, -s)
    t3 = obj.add_vertex(s, h_slab, s)
    t4 = obj.add_vertex(-s, h_slab, s)

    obj.add_quad(b4, b3, b2, b1)
    obj.add_quad(b1, b2, t2, t1)
    obj.add_quad(b2, b3, t3, t2)
    obj.add_quad(b3, b4, t4, t3)
    obj.add_quad(b4, b1, t1, t4)
    obj.add_quad(t1, t2, t3, t4)

    # 5 Spikes (center + 4 offsets)
    spike_positions = [
        (0, 0, 1.2),
        (-0.5, -0.5, 0.9),
        (0.5, -0.5, 0.9),
        (-0.5, 0.5, 0.9),
        (0.5, 0.5, 0.9),
    ]

    for sx, sz, sh in spike_positions:
        r = 0.18
        apex = obj.add_vertex(sx, h_slab + sh, sz)
        sp1 = obj.add_vertex(sx - r, h_slab, sz - r)
        sp2 = obj.add_vertex(sx + r, h_slab, sz - r)
        sp3 = obj.add_vertex(sx + r, h_slab, sz + r)
        sp4 = obj.add_vertex(sx - r, h_slab, sz + r)

        obj.add_tri(sp1, sp2, apex)
        obj.add_tri(sp2, sp3, apex)
        obj.add_tri(sp3, sp4, apex)
        obj.add_tri(sp4, sp1, apex)

    obj.save()

# 8. Enemy Drone Sentinel (Hovering Relic)
def build_enemy_sentinel():
    obj = ObjBuilder("Enemy_VoidSentinel")
    # Central sphere-like polyhedron (Dodecahedron/Faceted head)
    core_r = 0.6
    # 8-sided core
    c_top = obj.add_vertex(0, core_r*1.1, 0)
    c_bot = obj.add_vertex(0, -core_r*1.1, 0)
    c_mid = []
    for i in range(8):
        ang = i * (2.0 * math.pi / 8)
        c_mid.append(obj.add_vertex(core_r * math.cos(ang), 0, core_r * math.sin(ang)))

    for i in range(8):
        nxt = (i + 1) % 8
        obj.add_tri(c_top, c_mid[i], c_mid[nxt])
        obj.add_tri(c_bot, c_mid[nxt], c_mid[i])

    # Optic Eye (front cone)
    eye_tip = obj.add_vertex(0, 0, core_r + 0.3)
    eye_1 = obj.add_vertex(0.2, 0.2, core_r*0.8)
    eye_2 = obj.add_vertex(-0.2, 0.2, core_r*0.8)
    eye_3 = obj.add_vertex(-0.2, -0.2, core_r*0.8)
    eye_4 = obj.add_vertex(0.2, -0.2, core_r*0.8)
    obj.add_tri(eye_1, eye_tip, eye_2)
    obj.add_tri(eye_2, eye_tip, eye_3)
    obj.add_tri(eye_3, eye_tip, eye_4)
    obj.add_tri(eye_4, eye_tip, eye_1)

    # Hover Wings (Left & Right fins)
    for side in [-1, 1]:
        w_root_f = obj.add_vertex(side * core_r * 0.8, 0.1, 0.2)
        w_root_b = obj.add_vertex(side * core_r * 0.8, 0.1, -0.3)
        w_tip = obj.add_vertex(side * 1.3, -0.2, -0.2)
        w_bot = obj.add_vertex(side * core_r * 0.8, -0.2, -0.05)

        obj.add_tri(w_root_f, w_tip, w_root_b)
        obj.add_tri(w_root_f, w_bot, w_tip)
        obj.add_tri(w_root_b, w_tip, w_bot)

    # Crown halo ring
    halo_y = core_r * 0.8
    halo_r = 0.5
    h_v = []
    for i in range(6):
        ang = i * (2.0 * math.pi / 6)
        h_v.append(obj.add_vertex(halo_r * math.cos(ang), halo_y, halo_r * math.sin(ang)))
    for i in range(6):
        nxt = (i + 1) % 6
        obj.add_tri(c_top, h_v[i], h_v[nxt])

    obj.save()

# 9. Grand Victory Shrine Portal
def build_victory_shrine():
    obj = ObjBuilder("Portal_VictoryGate")
    # Base pedestal
    bw, bl, bh = 2.5, 2.0, 0.4
    obj.add_quad(
        obj.add_vertex(-bw, 0, -bl),
        obj.add_vertex(bw, 0, -bl),
        obj.add_vertex(bw, 0, bl),
        obj.add_vertex(-bw, 0, bl)
    )
    p_t1 = obj.add_vertex(-bw, bh, -bl)
    p_t2 = obj.add_vertex(bw, bh, -bl)
    p_t3 = obj.add_vertex(bw, bh, bl)
    p_t4 = obj.add_vertex(-bw, bh, bl)
    obj.add_quad(p_t1, p_t2, p_t3, p_t4)

    # Left & Right Arch Pillars
    arch_h = 4.2
    pw, pl = 0.45, 0.55
    for side in [-1.5, 1.5]:
        b_1 = obj.add_vertex(side - pw, bh, -pl)
        b_2 = obj.add_vertex(side + pw, bh, -pl)
        b_3 = obj.add_vertex(side + pw, bh, pl)
        b_4 = obj.add_vertex(side - pw, bh, pl)

        t_1 = obj.add_vertex(side - pw, arch_h, -pl)
        t_2 = obj.add_vertex(side + pw, arch_h, -pl)
        t_3 = obj.add_vertex(side + pw, arch_h, pl)
        t_4 = obj.add_vertex(side - pw, arch_h, pl)

        obj.add_quad(b_1, b_2, t_2, t_1)
        obj.add_quad(b_2, b_3, t_3, t_2)
        obj.add_quad(b_3, b_4, t_4, t_3)
        obj.add_quad(b_4, b_1, t_1, t_4)

    # Top Arch Keystone
    k_1 = obj.add_vertex(-1.5 - pw, arch_h, -pl)
    k_2 = obj.add_vertex(1.5 + pw, arch_h, -pl)
    k_3 = obj.add_vertex(1.5 + pw, arch_h, pl)
    k_4 = obj.add_vertex(-1.5 - pw, arch_h, pl)

    kt_1 = obj.add_vertex(-1.2, arch_h + 0.8, -pl)
    kt_2 = obj.add_vertex(1.2, arch_h + 0.8, -pl)
    kt_3 = obj.add_vertex(1.2, arch_h + 0.8, pl)
    kt_4 = obj.add_vertex(-1.2, arch_h + 0.8, pl)

    obj.add_quad(k_1, k_2, kt_2, kt_1)
    obj.add_quad(k_2, k_3, kt_3, kt_2)
    obj.add_quad(k_3, k_4, kt_4, kt_3)
    obj.add_quad(k_4, k_1, kt_1, kt_4)
    obj.add_quad(kt_1, kt_2, kt_3, kt_4)

    # Center Energy Portal Disk
    v_center = obj.add_vertex(0, (bh + arch_h)*0.55, 0)
    segs = 12
    v_disc = []
    p_radius = 1.3
    for i in range(segs):
        ang = i * (2.0 * math.pi / segs)
        v_disc.append(obj.add_vertex(p_radius * math.cos(ang), (bh + arch_h)*0.55 + p_radius * math.sin(ang), 0))

    for i in range(segs):
        nxt = (i + 1) % segs
        obj.add_tri(v_center, v_disc[i], v_disc[nxt])
        obj.add_tri(v_center, v_disc[nxt], v_disc[i])

    obj.save()

# 10. Vacuum Poolable Power Cell
def build_power_cell():
    obj = ObjBuilder("Prop_PowerCell")
    r_core = 0.35
    r_cap = 0.42
    h_core = 0.6
    h_cap = 0.15

    # Center capsule cylinder
    segs = 8
    t_ring = []
    b_ring = []
    for i in range(segs):
        ang = i * (2.0 * math.pi / segs)
        t_ring.append(obj.add_vertex(r_core * math.cos(ang), h_core*0.5, r_core * math.sin(ang)))
        b_ring.append(obj.add_vertex(r_core * math.cos(ang), -h_core*0.5, r_core * math.sin(ang)))

    for i in range(segs):
        nxt = (i + 1) % segs
        obj.add_quad(t_ring[i], b_ring[i], b_ring[nxt], t_ring[nxt])

    # Top Cap (reinforced)
    c_top = obj.add_vertex(0, h_core*0.5 + h_cap, 0)
    cap_ring = []
    for i in range(segs):
        ang = i * (2.0 * math.pi / segs)
        cap_ring.append(obj.add_vertex(r_cap * math.cos(ang), h_core*0.5 + h_cap*0.5, r_cap * math.sin(ang)))

    for i in range(segs):
        nxt = (i + 1) % segs
        obj.add_quad(t_ring[i], cap_ring[i], cap_ring[nxt], t_ring[nxt])
        obj.add_tri(c_top, cap_ring[i], cap_ring[nxt])

    # Bottom Cap
    c_bot = obj.add_vertex(0, -h_core*0.5 - h_cap, 0)
    bcap_ring = []
    for i in range(segs):
        ang = i * (2.0 * math.pi / segs)
        bcap_ring.append(obj.add_vertex(r_cap * math.cos(ang), -h_core*0.5 - h_cap*0.5, r_cap * math.sin(ang)))

    for i in range(segs):
        nxt = (i + 1) % segs
        obj.add_quad(b_ring[i], bcap_ring[i], bcap_ring[nxt], b_ring[nxt])
        obj.add_tri(c_bot, bcap_ring[nxt], bcap_ring[i])

    obj.save()

# 11. Checkpoint Beacon Monolith
def build_checkpoint_beacon():
    obj = ObjBuilder("Checkpoint_Monolith")
    # Tapered pedestal
    w_b, w_t = 0.6, 0.35
    h_p = 1.8
    b1 = obj.add_vertex(-w_b, 0, -w_b)
    b2 = obj.add_vertex(w_b, 0, -w_b)
    b3 = obj.add_vertex(w_b, 0, w_b)
    b4 = obj.add_vertex(-w_b, 0, w_b)

    t1 = obj.add_vertex(-w_t, h_p, -w_t)
    t2 = obj.add_vertex(w_t, h_p, -w_t)
    t3 = obj.add_vertex(w_t, h_p, w_t)
    t4 = obj.add_vertex(-w_t, h_p, w_t)

    obj.add_quad(b4, b3, b2, b1)
    obj.add_quad(b1, b2, t2, t1)
    obj.add_quad(b2, b3, t3, t2)
    obj.add_quad(b3, b4, t4, t3)
    obj.add_quad(b4, b1, t1, t4)
    obj.add_quad(t1, t2, t3, t4)

    # Floating beacon crystal atop pedestal
    gem_y = h_p + 0.6
    gem_r = 0.28
    gem_h = 0.45
    g_top = obj.add_vertex(0, gem_y + gem_h, 0)
    g_bot = obj.add_vertex(0, gem_y - gem_h, 0)
    g1 = obj.add_vertex(gem_r, gem_y, 0)
    g2 = obj.add_vertex(0, gem_y, gem_r)
    g3 = obj.add_vertex(-gem_r, gem_y, 0)
    g4 = obj.add_vertex(0, gem_y, -gem_r)

    obj.add_tri(g_top, g1, g2)
    obj.add_tri(g_top, g2, g3)
    obj.add_tri(g_top, g3, g4)
    obj.add_tri(g_top, g4, g1)

    obj.add_tri(g_bot, g2, g1)
    obj.add_tri(g_bot, g3, g2)
    obj.add_tri(g_bot, g4, g3)
    obj.add_tri(g_bot, g1, g4)

    obj.save()

# 12. 3D Faceted Star Relic
def build_star_relic():
    obj = ObjBuilder("Star_Relic3D")
    r_outer = 0.65
    r_inner = 0.32
    thick = 0.2

    # Front apex, Back apex
    f_apex = obj.add_vertex(0, 0, thick)
    b_apex = obj.add_vertex(0, 0, -thick)

    # 10 perimeter vertices
    perim = []
    for i in range(10):
        ang = i * (math.pi / 5.0) + math.pi*0.5
        r = r_outer if (i % 2 == 0) else r_inner
        perim.append(obj.add_vertex(r * math.cos(ang), r * math.sin(ang), 0))

    for i in range(10):
        nxt = (i + 1) % 10
        obj.add_tri(f_apex, perim[i], perim[nxt])
        obj.add_tri(b_apex, perim[nxt], perim[i])

    obj.save()

if __name__ == "__main__":
    build_hex_platform()
    build_pathway_stone()
    build_round_stepping()
    build_obelisk()
    build_grapple_anchor()
    build_plasma_saw()
    build_spike_trap()
    build_enemy_sentinel()
    build_victory_shrine()
    build_power_cell()
    build_checkpoint_beacon()
    build_star_relic()
    print("All professional 3D models generated successfully!")
