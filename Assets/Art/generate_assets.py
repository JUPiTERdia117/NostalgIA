import math
import os

def normalize(v):
    mag = math.sqrt(v[0]**2 + v[1]**2 + v[2]**2)
    if mag == 0: return (0, 1, 0)
    return (v[0]/mag, v[1]/mag, v[2]/mag)

def cross(a, b):
    return (
        a[1]*b[2] - a[2]*b[1],
        a[2]*b[0] - a[0]*b[2],
        a[0]*b[1] - a[1]*b[0]
    )

def sub(a, b):
    return (a[0]-b[0], a[1]-b[1], a[2]-b[2])

def write_obj_file(filepath, name, vertices, faces):
    # Compute per-face normal or per-vertex normal
    with open(filepath, 'w') as f:
        f.write(f"# Stylized 3D Model: {name}\n")
        f.write(f"o {name}\n")
        for v in vertices:
            f.write(f"v {v[0]:.4f} {v[1]:.4f} {v[2]:.4f}\n")
        f.write("vt 0.0 0.0\n")
        f.write("vt 1.0 0.0\n")
        f.write("vt 1.0 1.0\n")
        f.write("vt 0.0 1.0\n")
        
        # Write normals
        for face in faces:
            v0 = vertices[face[0]]
            v1 = vertices[face[1]]
            v2 = vertices[face[2]]
            n = normalize(cross(sub(v1, v0), sub(v2, v0)))
            f.write(f"vn {n[0]:.4f} {n[1]:.4f} {n[2]:.4f}\n")
            
        f.write(f"s 1\n")
        for i, face in enumerate(faces):
            ni = i + 1
            face_str = " ".join([f"{idx + 1}/1/{ni}" for idx in face])
            f.write(f"f {face_str}\n")
    print(f"Generated {filepath} ({len(vertices)} verts, {len(faces)} faces)")

out_dir = "Assets/Art/Models"
os.makedirs(out_dir, exist_ok=True)

# 1. Hexagonal Floating Platform
def gen_hex_platform():
    verts = []
    faces = []
    r_top = 3.5
    r_mid = 3.8
    r_bot = 0.5
    y_top = 0.0
    y_rim = -0.5
    y_cliff = -1.8
    y_tip = -4.5
    
    verts.append((0, y_top, 0)) # 0 center
    for i in range(6):
        ang = i * math.pi / 3.0
        verts.append((r_top * math.cos(ang), y_top, r_top * math.sin(ang))) # 1..6
    for i in range(6):
        faces.append([0, i + 1, ((i + 1) % 6) + 1])
        
    for i in range(6):
        ang = i * math.pi / 3.0
        verts.append((r_mid * math.cos(ang), y_rim, r_mid * math.sin(ang))) # 7..12
    for i in range(6):
        t1 = i + 1
        t2 = ((i + 1) % 6) + 1
        m1 = i + 7
        m2 = ((i + 1) % 6) + 7
        faces.append([t1, m1, m2])
        faces.append([t1, m2, t2])

    for i in range(6):
        ang = (i + 0.5) * math.pi / 3.0
        verts.append((r_mid * 0.85 * math.cos(ang), y_cliff, r_mid * 0.85 * math.sin(ang))) # 13..18
    for i in range(6):
        m1 = i + 7
        m2 = ((i + 1) % 6) + 7
        c1 = i + 13
        c2 = ((i + 1) % 6) + 13
        faces.append([m1, c1, c2])
        faces.append([m1, c2, m2])

    tip_idx = len(verts)
    verts.append((0, y_tip, 0)) # bottom tip
    for i in range(6):
        c1 = i + 13
        c2 = ((i + 1) % 6) + 13
        faces.append([c1, tip_idx, c2])
        
    write_obj_file(f"{out_dir}/Island_Hex_Platform.obj", "Island_Hex_Platform", verts, faces)

# 2. Large Island Plateau
def gen_island_plateau():
    verts = []
    faces = []
    n_pts = 16
    verts.append((0, 0.5, 0)) # 0: center top
    
    # Radii variation
    radii = [12.0 + 3.0 * math.sin(i * 1.8) + 1.5 * math.cos(i * 3.5) for i in range(n_pts)]
    
    # Top vertices
    for i in range(n_pts):
        ang = i * 2.0 * math.pi / n_pts
        verts.append((radii[i] * math.cos(ang), 0.5, radii[i] * math.sin(ang))) # 1..16
    for i in range(n_pts):
        faces.append([0, i + 1, ((i + 1) % n_pts) + 1])
        
    # Rim bevel
    rim_start = len(verts)
    for i in range(n_pts):
        ang = i * 2.0 * math.pi / n_pts
        verts.append(((radii[i] + 0.8) * math.cos(ang), -0.4, (radii[i] + 0.8) * math.sin(ang)))
    for i in range(n_pts):
        t1 = i + 1
        t2 = ((i + 1) % n_pts) + 1
        r1 = rim_start + i
        r2 = rim_start + ((i + 1) % n_pts)
        faces.append([t1, r1, r2])
        faces.append([t1, r2, t2])
        
    # Cliff mid
    cliff_start = len(verts)
    for i in range(n_pts):
        ang = i * 2.0 * math.pi / n_pts
        r = radii[i] * 0.75 + math.sin(i * 2.0)
        verts.append((r * math.cos(ang), -3.0, r * math.sin(ang)))
    for i in range(n_pts):
        r1 = rim_start + i
        r2 = rim_start + ((i + 1) % n_pts)
        c1 = cliff_start + i
        c2 = cliff_start + ((i + 1) % n_pts)
        faces.append([r1, c1, c2])
        faces.append([r1, c2, r2])

    # Bottom rocky stalactite roots
    root_tip = len(verts)
    verts.append((0.5, -9.0, -0.5))
    for i in range(n_pts):
        c1 = cliff_start + i
        c2 = cliff_start + ((i + 1) % n_pts)
        faces.append([c1, root_tip, c2])

    write_obj_file(f"{out_dir}/Island_Large_Plateau.obj", "Island_Large_Plateau", verts, faces)

# 3. Arena Island (Round combat arena)
def gen_island_arena():
    verts = []
    faces = []
    n = 16
    r_inner = 8.0
    r_wall = 8.8
    h_wall = 1.0
    
    verts.append((0, 0, 0)) # 0 center
    for i in range(n):
        ang = i * 2.0 * math.pi / n
        verts.append((r_inner * math.cos(ang), 0, r_inner * math.sin(ang))) # 1..n
    for i in range(n):
        faces.append([0, i + 1, ((i + 1) % n) + 1])
        
    # Parapet wall top
    w_start = len(verts)
    for i in range(n):
        ang = i * 2.0 * math.pi / n
        verts.append((r_inner * math.cos(ang), h_wall, r_inner * math.sin(ang)))
    for i in range(n):
        b1 = i + 1
        b2 = ((i + 1) % n) + 1
        w1 = w_start + i
        w2 = w_start + ((i + 1) % n)
        faces.append([b1, w1, w2])
        faces.append([b1, w2, b2])

    # Outer wall rim
    o_start = len(verts)
    for i in range(n):
        ang = i * 2.0 * math.pi / n
        verts.append((r_wall * math.cos(ang), h_wall - 0.2, r_wall * math.sin(ang)))
    for i in range(n):
        w1 = w_start + i
        w2 = w_start + ((i + 1) % n)
        o1 = o_start + i
        o2 = o_start + ((i + 1) % n)
        faces.append([w1, o1, o2])
        faces.append([w1, o2, w2])

    # Under cliff
    u_start = len(verts)
    for i in range(n):
        ang = i * 2.0 * math.pi / n
        verts.append((r_wall * 0.6 * math.cos(ang), -3.5, r_wall * 0.6 * math.sin(ang)))
    for i in range(n):
        o1 = o_start + i
        o2 = o_start + ((i + 1) % n)
        u1 = u_start + i
        u2 = u_start + ((i + 1) % n)
        faces.append([o1, u1, u2])
        faces.append([o1, u2, o2])

    # Bottom tip
    tip = len(verts)
    verts.append((0, -6.5, 0))
    for i in range(n):
        u1 = u_start + i
        u2 = u_start + ((i + 1) % n)
        faces.append([u1, tip, u2])

    write_obj_file(f"{out_dir}/Island_Arena.obj", "Island_Arena", verts, faces)

# 4. Grapple Beacon Anchor (Floating crystal gyroscope)
def gen_grapple_beacon():
    verts = []
    faces = []
    # Octahedron central core
    # 0: top, 1: bot, 2..5: equator
    verts.append((0, 1.2, 0))
    verts.append((0, -1.2, 0))
    verts.append((0.7, 0, 0))
    verts.append((0, 0, 0.7))
    verts.append((-0.7, 0, 0))
    verts.append((0, 0, -0.7))
    
    faces.append([0, 2, 3])
    faces.append([0, 3, 4])
    faces.append([0, 4, 5])
    faces.append([0, 5, 2])
    faces.append([1, 3, 2])
    faces.append([1, 4, 3])
    faces.append([1, 5, 4])
    faces.append([1, 2, 5])
    
    # Outer gimbal ring
    n_ring = 12
    r_in = 1.3
    r_out = 1.7
    ring_start = len(verts)
    for i in range(n_ring):
        ang = i * 2.0 * math.pi / n_ring
        verts.append((r_in * math.cos(ang), 0.1, r_in * math.sin(ang)))
        verts.append((r_out * math.cos(ang), 0.1, r_out * math.sin(ang)))
        verts.append((r_in * math.cos(ang), -0.1, r_in * math.sin(ang)))
        verts.append((r_out * math.cos(ang), -0.1, r_out * math.sin(ang)))

    for i in range(n_ring):
        c = ring_start + i * 4
        nxt = ring_start + ((i + 1) % n_ring) * 4
        # Top face
        faces.append([c+0, c+1, nxt+1])
        faces.append([c+0, nxt+1, nxt+0])
        # Bottom face
        faces.append([c+2, nxt+3, c+3])
        faces.append([c+2, nxt+2, nxt+3])
        # Outer rim
        faces.append([c+1, c+3, nxt+3])
        faces.append([c+1, nxt+3, nxt+1])
        # Inner rim
        faces.append([c+0, nxt+2, c+2])
        faces.append([c+0, nxt+0, nxt+2])

    write_obj_file(f"{out_dir}/Grapple_Beacon.obj", "Grapple_Beacon", verts, faces)

# 5. Hazard Spike Trap (Plate with 9 sharp spikes)
def gen_spike_trap():
    verts = []
    faces = []
    # Base plate box 2x2, height 0.2
    s = 1.2
    h = 0.2
    # Base top
    v_base = [
        (-s, h, -s), (s, h, -s), (s, h, s), (-s, h, s),
        (-s, 0, -s), (s, 0, -s), (s, 0, s), (-s, 0, s)
    ]
    verts.extend(v_base)
    faces.append([0, 1, 2])
    faces.append([0, 2, 3])
    faces.append([4, 6, 5])
    faces.append([4, 7, 6])
    faces.append([0, 4, 5])
    faces.append([0, 5, 1])
    faces.append([1, 5, 6])
    faces.append([1, 6, 2])
    faces.append([2, 6, 7])
    faces.append([2, 7, 3])
    faces.append([3, 7, 4])
    faces.append([3, 4, 0])
    
    # 9 pyramid spikes
    positions = [
        (-0.7, -0.7), (0.0, -0.7), (0.7, -0.7),
        (-0.7,  0.0), (0.0,  0.0), (0.7,  0.0),
        (-0.7,  0.7), (0.0,  0.7), (0.7,  0.7)
    ]
    spike_w = 0.22
    spike_h = 0.95
    for px, pz in positions:
        st = len(verts)
        verts.append((px - spike_w, h, pz - spike_w))
        verts.append((px + spike_w, h, pz - spike_w))
        verts.append((px + spike_w, h, pz + spike_w))
        verts.append((px - spike_w, h, pz + spike_w))
        verts.append((px, h + spike_h, pz)) # peak
        faces.append([st+0, st+4, st+1])
        faces.append([st+1, st+4, st+2])
        faces.append([st+2, st+4, st+3])
        faces.append([st+3, st+4, st+0])

    write_obj_file(f"{out_dir}/Hazard_Spike_Trap.obj", "Hazard_Spike_Trap", verts, faces)

# 6. Spinning Saw Blade
def gen_spinning_saw():
    verts = []
    faces = []
    teeth = 8
    r_core = 0.4
    r_valley = 1.0
    r_tip = 1.6
    thick = 0.1
    
    # Generate 2D profile
    profile = []
    for i in range(teeth):
        ang_valley = i * 2.0 * math.pi / teeth
        ang_tip = (i + 0.45) * 2.0 * math.pi / teeth
        profile.append((r_valley * math.cos(ang_valley), r_valley * math.sin(ang_valley)))
        profile.append((r_tip * math.cos(ang_tip), r_tip * math.sin(ang_tip)))
        
    n_p = len(profile)
    # Front center and back center
    verts.append((0, thick, 0)) # 0
    verts.append((0, -thick, 0)) # 1
    
    front_start = len(verts)
    for px, pz in profile:
        verts.append((px, thick, pz))
    back_start = len(verts)
    for px, pz in profile:
        verts.append((px, -thick, pz))
        
    for i in range(n_p):
        f1 = front_start + i
        f2 = front_start + ((i + 1) % n_p)
        faces.append([0, f1, f2])
        
        b1 = back_start + i
        b2 = back_start + ((i + 1) % n_p)
        faces.append([1, b2, b1])
        
        faces.append([f1, b1, b2])
        faces.append([f1, b2, f2])
        
    write_obj_file(f"{out_dir}/Hazard_Spinning_Saw.obj", "Hazard_Spinning_Saw", verts, faces)

# 7. Laser Pylon
def gen_laser_pylon():
    verts = []
    faces = []
    # Base hexagon, tapered mid, bulb emitter
    h_base = 0.4
    r_base = 0.9
    h_top = 2.4
    r_top = 0.35
    n = 6
    verts.append((0, 0, 0)) # 0 bot center
    for i in range(n):
        ang = i * 2.0 * math.pi / n
        verts.append((r_base * math.cos(ang), 0, r_base * math.sin(ang)))
    for i in range(n):
        faces.append([0, ((i+1)%n)+1, i+1])
        
    for i in range(n):
        ang = i * 2.0 * math.pi / n
        verts.append((r_base * 0.8 * math.cos(ang), h_base, r_base * 0.8 * math.sin(ang))) # 7..12
    for i in range(n):
        b1 = i + 1
        b2 = ((i+1)%n) + 1
        t1 = i + 7
        t2 = ((i+1)%n) + 7
        faces.append([b1, t1, t2])
        faces.append([b1, t2, b2])
        
    for i in range(n):
        ang = i * 2.0 * math.pi / n
        verts.append((r_top * math.cos(ang), h_top, r_top * math.sin(ang))) # 13..18
    for i in range(n):
        b1 = i + 7
        b2 = ((i+1)%n) + 7
        t1 = i + 13
        t2 = ((i+1)%n) + 13
        faces.append([b1, t1, t2])
        faces.append([b1, t2, b2])

    # Emitter sphere peak
    emitter_tip = len(verts)
    verts.append((0, h_top + 0.4, 0))
    for i in range(n):
        t1 = i + 13
        t2 = ((i+1)%n) + 13
        faces.append([t1, emitter_tip, t2])
        
    write_obj_file(f"{out_dir}/Hazard_Laser_Pylon.obj", "Hazard_Laser_Pylon", verts, faces)

# 8. Enemy Sentinel Drone
def gen_sentinel_drone():
    verts = []
    faces = []
    # Spherical body
    # Using icosahedron / truncated sphere
    r_body = 0.75
    n_rings = 5
    n_seg = 8
    for ri in range(n_rings + 1):
        phi = ri * math.pi / n_rings
        y = r_body * math.cos(phi)
        r_slice = r_body * math.sin(phi)
        for si in range(n_seg):
            theta = si * 2.0 * math.pi / n_seg
            x = r_slice * math.cos(theta)
            z = r_slice * math.sin(theta)
            verts.append((x, y, z))
            
    for ri in range(n_rings):
        for si in range(n_seg):
            cur = ri * n_seg + si
            nxt = ri * n_seg + ((si + 1) % n_seg)
            cur_down = (ri + 1) * n_seg + si
            nxt_down = (ri + 1) * n_seg + ((si + 1) % n_seg)
            faces.append([cur, cur_down, nxt_down])
            faces.append([cur, nxt_down, nxt])

    # Front Visor (Eye lens protruding in +Z)
    eye_start = len(verts)
    r_eye = 0.38
    z_eye = 0.65
    verts.append((0, 0, z_eye + 0.25)) # 0: eye apex
    for i in range(8):
        ang = i * 2.0 * math.pi / 8
        verts.append((r_eye * math.cos(ang), r_eye * math.sin(ang), z_eye))
    for i in range(8):
        faces.append([eye_start, eye_start + 1 + i, eye_start + 1 + ((i+1)%8)])

    # Left & Right Thruster Wings
    for side in [-1, 1]:
        w_start = len(verts)
        verts.append((side * 0.7, 0.1, 0.2))
        verts.append((side * 1.5, 0.2, -0.4))
        verts.append((side * 1.4, -0.1, -0.5))
        verts.append((side * 0.7, -0.1, 0.1))
        if side == 1:
            faces.append([w_start+0, w_start+1, w_start+2])
            faces.append([w_start+0, w_start+2, w_start+3])
        else:
            faces.append([w_start+0, w_start+2, w_start+1])
            faces.append([w_start+0, w_start+3, w_start+2])

    write_obj_file(f"{out_dir}/Enemy_Sentinel_Drone.obj", "Enemy_Sentinel_Drone", verts, faces)

# 9. Poolable Power Cell (Beveled cube with glowing core)
def gen_power_cell():
    verts = []
    faces = []
    # Chamfered cube of size 0.9
    s = 0.45
    c = 0.32
    # 24 vertices for chamfered corners
    pts = [
        # +Y face
        (-c, s, -c), (c, s, -c), (c, s, c), (-c, s, c),
        # -Y face
        (-c, -s, -c), (c, -s, -c), (c, -s, c), (-c, -s, c),
        # +X face
        (s, -c, -c), (s, c, -c), (s, c, c), (s, -c, c),
        # -X face
        (-s, -c, -c), (-s, c, -c), (-s, c, c), (-s, -c, c),
        # +Z face
        (-c, -c, s), (c, -c, s), (c, c, s), (-c, c, s),
        # -Z face
        (-c, -c, -s), (c, -c, -s), (c, c, -s), (-c, c, -s)
    ]
    verts.extend(pts)
    # Face quads
    # Top (+Y)
    faces.append([0, 1, 2])
    faces.append([0, 2, 3])
    # Bot (-Y)
    faces.append([4, 6, 5])
    faces.append([4, 7, 6])
    # +X
    faces.append([8, 9, 10])
    faces.append([8, 10, 11])
    # -X
    faces.append([12, 14, 13])
    faces.append([12, 15, 14])
    # +Z
    faces.append([16, 17, 18])
    faces.append([16, 18, 19])
    # -Z
    faces.append([20, 22, 21])
    faces.append([20, 23, 22])
    
    # Bridge edges (top to sides)
    faces.append([0, 23, 22])
    faces.append([0, 22, 1])
    faces.append([3, 2, 18])
    faces.append([3, 18, 19])
    faces.append([1, 9, 10])
    faces.append([1, 10, 2])
    faces.append([0, 3, 14])
    faces.append([0, 14, 13])

    write_obj_file(f"{out_dir}/Poolable_PowerCell.obj", "Poolable_PowerCell", verts, faces)

# 10. Victory Portal Arch
def gen_victory_portal():
    verts = []
    faces = []
    # Stepped circular base dais
    r_dais = 4.0
    h_dais = 0.5
    n_d = 16
    verts.append((0, h_dais, 0)) # 0 center
    for i in range(n_d):
        ang = i * 2.0 * math.pi / n_d
        verts.append((r_dais * math.cos(ang), h_dais, r_dais * math.sin(ang)))
    for i in range(n_d):
        faces.append([0, i+1, ((i+1)%n_d)+1])
    for i in range(n_d):
        ang = i * 2.0 * math.pi / n_d
        verts.append(((r_dais + 0.3) * math.cos(ang), 0, (r_dais + 0.3) * math.sin(ang)))
    for i in range(n_d):
        t1 = i + 1
        t2 = ((i+1)%n_d) + 1
        b1 = n_d + 1 + i
        b2 = n_d + 1 + ((i+1)%n_d)
        faces.append([t1, b1, b2])
        faces.append([t1, b2, t2])
        
    # Twin Portal Pillars (Left at X = -2.2, Right at X = 2.2)
    for px in [-2.2, 2.2]:
        p_start = len(verts)
        pw = 0.5
        ph = 5.0
        # Box pillar
        v_box = [
            (px - pw, h_dais, -pw), (px + pw, h_dais, -pw), (px + pw, h_dais, pw), (px - pw, h_dais, pw),
            (px - pw, ph, -pw), (px + pw, ph, -pw), (px + pw, ph, pw), (px - pw, ph, pw)
        ]
        verts.extend(v_box)
        faces.append([p_start+0, p_start+4, p_start+5])
        faces.append([p_start+0, p_start+5, p_start+1])
        faces.append([p_start+1, p_start+5, p_start+6])
        faces.append([p_start+1, p_start+6, p_start+2])
        faces.append([p_start+2, p_start+6, p_start+7])
        faces.append([p_start+2, p_start+7, p_start+3])
        faces.append([p_start+3, p_start+7, p_start+4])
        faces.append([p_start+3, p_start+4, p_start+0])

    # Arch crossbeam between pillars at Y=4.8 to 5.4
    a_start = len(verts)
    aw = 2.8
    ay1 = 4.8
    ay2 = 5.4
    az = 0.6
    v_arch = [
        (-aw, ay1, -az), (aw, ay1, -az), (aw, ay1, az), (-aw, ay1, az),
        (-aw, ay2, -az), (aw, ay2, -az), (aw, ay2, az), (-aw, ay2, az)
    ]
    verts.extend(v_arch)
    faces.append([a_start+4, a_start+5, a_start+6])
    faces.append([a_start+4, a_start+6, a_start+7])
    faces.append([a_start+0, a_start+2, a_start+1])
    faces.append([a_start+0, a_start+3, a_start+2])
    faces.append([a_start+0, a_start+4, a_start+7])
    faces.append([a_start+0, a_start+7, a_start+3])
    faces.append([a_start+1, a_start+6, a_start+5])
    faces.append([a_start+1, a_start+2, a_start+6])
    faces.append([a_start+0, a_start+5, a_start+4])
    faces.append([a_start+0, a_start+1, a_start+5])
    faces.append([a_start+3, a_start+7, a_start+6])
    faces.append([a_start+3, a_start+6, a_start+2])

    # Floating Portal Gateway Disc (Center X=0, Y=2.7, Z=0)
    disc_start = len(verts)
    r_disc = 1.8
    verts.append((0, 2.7, 0)) # center
    for i in range(12):
        ang = i * 2.0 * math.pi / 12
        verts.append((r_disc * math.sin(ang), 2.7 + r_disc * math.cos(ang), 0))
    for i in range(12):
        faces.append([disc_start, disc_start + 1 + i, disc_start + 1 + ((i+1)%12)])
        faces.append([disc_start, disc_start + 1 + ((i+1)%12), disc_start + 1 + i])

    write_obj_file(f"{out_dir}/Victory_Portal.obj", "Victory_Portal", verts, faces)

# 11. 3D Star Collectible
def gen_star_collectible():
    verts = []
    faces = []
    n = 5
    r_outer = 1.0
    r_inner = 0.42
    thick = 0.3
    
    verts.append((0, 0, thick))  # 0 front center
    verts.append((0, 0, -thick)) # 1 back center
    
    # 10 star points in XY plane
    p_start = len(verts)
    for i in range(n * 2):
        ang = i * math.pi / n + math.pi / 2.0
        r = r_outer if (i % 2 == 0) else r_inner
        verts.append((r * math.cos(ang), r * math.sin(ang), 0))
        
    for i in range(n * 2):
        cur = p_start + i
        nxt = p_start + ((i + 1) % (n * 2))
        faces.append([0, cur, nxt])
        faces.append([1, nxt, cur])

    write_obj_file(f"{out_dir}/Star_Collectible.obj", "Star_Collectible", verts, faces)

# 12. Checkpoint Obelisk
def gen_checkpoint_obelisk():
    verts = []
    faces = []
    # Stepped pedestal
    pw = 1.0
    ph = 0.3
    v_base = [
        (-pw, ph, -pw), (pw, ph, -pw), (pw, ph, pw), (-pw, ph, pw),
        (-pw*1.2, 0, -pw*1.2), (pw*1.2, 0, -pw*1.2), (pw*1.2, 0, pw*1.2), (-pw*1.2, 0, pw*1.2)
    ]
    verts.extend(v_base)
    faces.append([0, 1, 2])
    faces.append([0, 2, 3])
    faces.append([0, 4, 5])
    faces.append([0, 5, 1])
    faces.append([1, 5, 6])
    faces.append([1, 6, 2])
    faces.append([2, 6, 7])
    faces.append([2, 7, 3])
    faces.append([3, 7, 4])
    faces.append([3, 4, 0])
    
    # Tapered obelisk column
    ow1 = 0.45
    ow2 = 0.25
    oh = 2.8
    o_start = len(verts)
    v_col = [
        (-ow1, ph, -ow1), (ow1, ph, -ow1), (ow1, ph, ow1), (-ow1, ph, ow1),
        (-ow2, oh, -ow2), (ow2, oh, -ow2), (ow2, oh, ow2), (-ow2, oh, ow2),
        (0, oh + 0.5, 0) # pyramidion cap
    ]
    verts.extend(v_col)
    faces.append([o_start+0, o_start+4, o_start+5])
    faces.append([o_start+0, o_start+5, o_start+1])
    faces.append([o_start+1, o_start+5, o_start+6])
    faces.append([o_start+1, o_start+6, o_start+2])
    faces.append([o_start+2, o_start+6, o_start+7])
    faces.append([o_start+2, o_start+7, o_start+3])
    faces.append([o_start+3, o_start+7, o_start+4])
    faces.append([o_start+3, o_start+4, o_start+0])
    # Cap faces
    faces.append([o_start+4, o_start+8, o_start+5])
    faces.append([o_start+5, o_start+8, o_start+6])
    faces.append([o_start+6, o_start+8, o_start+7])
    faces.append([o_start+7, o_start+8, o_start+4])

    write_obj_file(f"{out_dir}/Checkpoint_Obelisk.obj", "Checkpoint_Obelisk", verts, faces)

# Run all generators
gen_hex_platform()
gen_island_plateau()
gen_island_arena()
gen_grapple_beacon()
gen_spike_trap()
gen_spinning_saw()
gen_laser_pylon()
gen_sentinel_drone()
gen_power_cell()
gen_victory_portal()
gen_star_collectible()
gen_checkpoint_obelisk()

print("All 12 3D models generated successfully!")
