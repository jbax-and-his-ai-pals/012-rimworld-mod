using System;
using System.Collections.Generic;

namespace Site44.Gen
{
    // Room shape library and room combinations (suites, interlocks, pairs, galleries, cubicle runs).
    // A band slice is described in slice coordinates (s along the corridor, t away from it, t = 0 touching the corridor wall).
    internal sealed partial class PlanBuild
    {
        const int KRect = 0, KL = 1, KT = 2, KPlus = 3, KBay = 4, KZ = 5, KStep = 6, KCham = 7, KOct = 8, KTrap = 9, KDisc = 10, KPillar = 11, KComb = 12, KInvT = 13;

        readonly bool[] rotundaDone = new bool[64];
        public int suiteCount;

        static void Cut(bool[] k, int w, int d, int s0, int s1, int t0, int t1)
        {
            for (int s = Math.Max(0, s0); s <= Math.Min(w - 1, s1); s++)
                for (int t = Math.Max(0, t0); t <= Math.Min(d - 1, t1); t++)
                    k[s * d + t] = false;
        }

        // Accepts a mask when it is one 4-connected piece, touches the corridor side with a run of 3, has no spurs and enough area.
        static bool ValidMask(bool[] k, int w, int d)
        {
            int count = 0, first = -1;
            for (int i = 0; i < k.Length; i++) if (k[i]) { count++; if (first < 0) first = i; }
            if (count < 12) return false;
            int run = 0, best = 0;
            for (int s = 0; s < w; s++)
            {
                run = k[s * d] ? run + 1 : 0;
                if (run > best) best = run;
            }

            if (best < 3) return false;
            for (int s = 0; s < w; s++)
                for (int t = 0; t < d; t++)
                {
                    if (!k[s * d + t]) continue;
                    int n = 0;
                    if (s > 0 && k[(s - 1) * d + t]) n++;
                    if (s < w - 1 && k[(s + 1) * d + t]) n++;
                    if (t > 0 && k[s * d + t - 1]) n++;
                    if (t < d - 1 && k[s * d + t + 1]) n++;
                    if (n <= 1) return false;
                }

            bool[] seen = new bool[k.Length];
            List<int> q = new List<int>();
            q.Add(first); seen[first] = true;
            for (int h = 0; h < q.Count; h++)
            {
                int c = q[h]; int s = c / d, t = c % d;
                if (s > 0 && k[c - d] && !seen[c - d]) { seen[c - d] = true; q.Add(c - d); }
                if (s < w - 1 && k[c + d] && !seen[c + d]) { seen[c + d] = true; q.Add(c + d); }
                if (t > 0 && k[c - 1] && !seen[c - 1]) { seen[c - 1] = true; q.Add(c - 1); }
                if (t < d - 1 && k[c + 1] && !seen[c + 1]) { seen[c + 1] = true; q.Add(c + 1); }
            }

            return q.Count == count;
        }

        bool[] MakeMask(int kind, int w, int d)
        {
            bool[] k = new bool[w * d];
            for (int i = 0; i < k.Length; i++) k[i] = true;
            switch (kind)
            {
                case KL:
                {
                    int bw = 2 + rng.Next(w - 4), bd = 2 + rng.Next(d - 3);
                    if (rng.Next(2) == 0) Cut(k, w, d, w - bw, w, d - bd, d); else Cut(k, w, d, 0, bw - 1, d - bd, d);
                    break;
                }
                case KT:
                case KInvT:
                {
                    int cb = 1 + rng.Next(Math.Max(1, (w - 3) / 2)), nd = 2 + rng.Next(d - 3);
                    if (w - 2 * cb < 3) return null;
                    if (kind == KT) { Cut(k, w, d, 0, cb - 1, d - nd, d); Cut(k, w, d, w - cb, w, d - nd, d); }
                    else { Cut(k, w, d, 0, cb - 1, 0, nd - 1); Cut(k, w, d, w - cb, w, 0, nd - 1); }
                    break;
                }
                case KPlus:
                {
                    int cb = 2 + rng.Next(Math.Max(1, (w - 5) / 2));
                    int nd = 2, fd = 2;
                    if (w - 2 * cb < 3 || nd + fd > d - 3) return null;
                    Cut(k, w, d, 0, cb - 1, 0, nd - 1); Cut(k, w, d, w - cb, w, 0, nd - 1);
                    Cut(k, w, d, 0, cb - 1, d - fd, d); Cut(k, w, d, w - cb, w, d - fd, d);
                    break;
                }
                case KBay:
                {
                    int bw = 2 + rng.Next(2), bdp = 2 + rng.Next(2);
                    if (d - bdp < 3) bdp = d - 3;
                    if (bdp < 1) return null;
                    int bs = Clamp((w - bw) / 2 + rng.Next(3) - 1, 2, w - bw - 2);
                    Cut(k, w, d, bs, bs + bw - 1, d - bdp, d);
                    break;
                }
                case KZ:
                {
                    int bw1 = 2 + rng.Next(Math.Max(1, w / 3 - 1)), bw2 = 2 + rng.Next(Math.Max(1, w / 3 - 1));
                    if (bw1 + bw2 > w - 3) return null;
                    int fd = 2 + rng.Next(Math.Max(1, d - 4)), nd = 2 + rng.Next(Math.Max(1, d - 4));
                    if (fd + nd > d - 1) return null;
                    Cut(k, w, d, 0, bw1 - 1, d - fd, d);
                    Cut(k, w, d, w - bw2, w, 0, nd - 1);
                    break;
                }
                case KStep:
                {
                    int sw = 2 + rng.Next(2), sd = 1 + rng.Next(2);
                    for (int s = 0; s < w; s++)
                    {
                        int edge = Math.Min(s, w - 1 - s);
                        int rm = Math.Max(0, 3 * sd - (edge / sw) * sd);
                        if (rm > d - 3) rm = d - 3;
                        if (rm > 0) Cut(k, w, d, s, s, d - rm, d);
                    }
                    break;
                }
                case KCham:
                {
                    int cs = 1 + rng.Next(3);
                    int corners = 1 + rng.Next(15);
                    for (int s = 0; s < w; s++)
                        for (int t = 0; t < d; t++)
                        {
                            if ((corners & 1) != 0 && s + (d - 1 - t) < cs) k[s * d + t] = false;
                            if ((corners & 2) != 0 && (w - 1 - s) + (d - 1 - t) < cs) k[s * d + t] = false;
                            if ((corners & 4) != 0 && s + t < cs) k[s * d + t] = false;
                            if ((corners & 8) != 0 && (w - 1 - s) + t < cs) k[s * d + t] = false;
                        }
                    break;
                }
                case KOct:
                {
                    int cs = 2 + rng.Next(2);
                    for (int s = 0; s < w; s++)
                        for (int t = 0; t < d; t++)
                            if (s + (d - 1 - t) < cs || (w - 1 - s) + (d - 1 - t) < cs || s + t < cs || (w - 1 - s) + t < cs) k[s * d + t] = false;
                    break;
                }
                case KTrap:
                {
                    int i0 = 2 + rng.Next(2);
                    for (int t = 0; t < d; t++)
                    {
                        int inset = (i0 * (d - 1 - t) + (d - 2)) / (d - 1);
                        Cut(k, w, d, 0, inset - 1, t, t);
                        Cut(k, w, d, w - inset, w, t, t);
                    }
                    break;
                }
                case KDisc:
                {
                    double rx = w / 2.0, ry = d / 2.0, cx = (w - 1) / 2.0, cy = (d - 1) / 2.0;
                    for (int s = 0; s < w; s++)
                        for (int t = 0; t < d; t++)
                        {
                            double ex = (s - cx) / rx, ey = (t - cy) / ry;
                            if (ex * ex + ey * ey > 1.05) k[s * d + t] = false;
                        }
                    break;
                }
                case KPillar:
                {
                    int s0 = 2 + rng.Next(2), step = 3 + rng.Next(2);
                    for (int s = s0; s <= w - 3; s += step)
                        for (int t = 2; t <= d - 3; t += 3) k[s * d + t] = false;
                    break;
                }
                case KComb:
                {
                    int off = rng.Next(4);
                    for (int s = 0; s < w; s++)
                        if (((s + off) % 4) >= 2) Cut(k, w, d, s, s, d - 2, d);
                    break;
                }
                default: return null;
            }

            return ValidMask(k, w, d) ? k : null;
        }

        static bool KindFits(int kind, int w, int d)
        {
            switch (kind)
            {
                case KL: return w >= 6 && d >= 5;
                case KT: case KInvT: return w >= 7 && d >= 5;
                case KPlus: return w >= 8 && d >= 7;
                case KBay: return w >= 6 && d >= 5;
                case KZ: return w >= 7 && d >= 6;
                case KStep: return w >= 7 && d >= 6;
                case KCham: return w >= 5 && d >= 5;
                case KOct: return w >= 7 && d >= 6;
                case KTrap: return w >= 8 && d >= 6;
                case KPillar: return w >= 7 && d >= 5;
                case KComb: return w >= 10 && d >= 5;
            }

            return false;
        }

        // Picks a shape class suited to the function (0 = plain rectangle).
        int ChooseKind(RoomFunction f, int w, int d)
        {
            if (w * d < 20 || w < 5 || d < 5) return KRect;
            int[] kinds; int[] wts; int rectW;
            switch (f)
            {
                case RoomFunction.OpenOffice: kinds = new[] { KL, KBay, KZ, KT, KStep, KPillar, KCham }; wts = new[] { 4, 3, 3, 2, 1, 2, 1 }; rectW = 1; break;
                case RoomFunction.Conference: kinds = new[] { KCham, KOct, KTrap }; wts = new[] { 3, 1, 1 }; rectW = 3; break;
                case RoomFunction.Cafeteria: kinds = new[] { KT, KPlus, KPillar, KBay }; wts = new[] { 3, 1, 2, 2 }; rectW = 2; break;
                case RoomFunction.ServerRoom: kinds = new[] { KPillar }; wts = new[] { 3 }; rectW = 3; break;
                case RoomFunction.PrivateOffice: kinds = new[] { KL, KCham, KBay, KInvT }; wts = new[] { 3, 3, 2, 1 }; rectW = 3; break;
                case RoomFunction.Storeroom:
                case RoomFunction.Archive: kinds = new[] { KL, KZ, KPillar, KInvT }; wts = new[] { 3, 2, 3, 1 }; rectW = 2; break;
                case RoomFunction.Lab: kinds = new[] { KL, KT, KBay, KCham, KZ }; wts = new[] { 3, 2, 2, 2, 1 }; rectW = 2; break;
                case RoomFunction.MachineHall: kinds = new[] { KPlus, KOct, KStep, KPillar, KT }; wts = new[] { 2, 2, 2, 3, 1 }; rectW = 2; break;
                case RoomFunction.Mechanical:
                case RoomFunction.Boiler: kinds = new[] { KL, KCham, KInvT }; wts = new[] { 2, 2, 1 }; rectW = 3; break;
                case RoomFunction.BreakRoom:
                case RoomFunction.Mailroom: kinds = new[] { KL, KBay, KCham }; wts = new[] { 2, 2, 2 }; rectW = 3; break;
                case RoomFunction.Ward:
                case RoomFunction.Bunkroom:
                case RoomFunction.Apartment: kinds = new[] { KL, KCham, KBay }; wts = new[] { 1, 2, 1 }; rectW = 6; break;
                case RoomFunction.OperatingRoom: kinds = new[] { KOct, KCham }; wts = new[] { 2, 2 }; rectW = 3; break;
                case RoomFunction.Atrium: kinds = new[] { KOct, KT }; wts = new[] { 3, 1 }; rectW = 1; break;
                case RoomFunction.Containment:
                case RoomFunction.Morgue:
                case RoomFunction.Pharmacy:
                case RoomFunction.Cell:
                case RoomFunction.Closet:
                case RoomFunction.Restroom:
                case RoomFunction.SecurityPost: return KRect;
                default: kinds = new[] { KL, KCham, KBay }; wts = new[] { 2, 2, 2 }; rectW = 3; break;
            }

            int sum = rectW;
            for (int i = 0; i < kinds.Length; i++) if (KindFits(kinds[i], w, d)) sum += wts[i];
            int r = rng.Next(sum);
            if (r < rectW) return KRect;
            r -= rectW;
            for (int i = 0; i < kinds.Length; i++)
            {
                if (!KindFits(kinds[i], w, d)) continue;
                r -= wts[i];
                if (r < 0) return kinds[i];
            }

            return KRect;
        }

        RoomFunction PickSmall(string arch)
        {
            for (int t = 0; t < 10; t++)
            {
                PIntent it = Pick(arch, false);
                if (it.Max <= 9 && it.F != RoomFunction.Containment && it.F != RoomFunction.OpenOffice && it.F != RoomFunction.Cafeteria && it.F != RoomFunction.MachineHall && it.F != RoomFunction.Conference) return it.F;
            }

            return RoomFunction.PrivateOffice;
        }

        static void VR(bool high, int vlo, int vhi, int t0, int t1, out int v0, out int v1)
        {
            if (high) { v0 = vlo + t0; v1 = vlo + t1; }
            else { v0 = vhi - t1; v1 = vhi - t0; }
        }

        int[] SplitWidths(int total, int parts, int min)
        {
            int[] v = new int[parts];
            int extra = total - parts * min;
            for (int i = 0; i < parts; i++) v[i] = min + extra / parts;
            int rem = extra - (extra / parts) * parts;
            for (int i = 0; i < rem; i++) v[rng.Next(parts)]++;
            return v;
        }

        // ------------------------------------------------------------------ band slicing
        void SliceSegment(PBar b, bool high, int u0, int u1, int vlo, int vhi)
        {
            int rem = u1 - u0 + 1, pos = u0, d = vhi - vlo + 1;
            bool social = b.Arch == "Office" || b.Arch == "Lab" || b.Arch == "Commons" || b.Arch == "Security" || b.Arch == "Medical" || b.Arch == "Barracks" || b.Arch == "Residential" || b.Arch == "Research";
            while (rem >= 3)
            {
                int roll = rng.Next(100), used = 0;
                if (b.Arch == "Medical" && d >= 6 && rem >= 11 && roll < 14) used = TryOperating(b, high, pos, rem, vlo, vhi);
                else if (social && d >= 6 && rem >= 11 && roll < 9) used = TrySuite(b, high, pos, rem, vlo, vhi);
                else if (social && d >= 5 && rem >= 13 && roll >= 9 && roll < 14) used = TryPair(b, high, pos, rem, vlo, vhi);
                else if (social && d >= 6 && rem >= 9 && roll >= 14 && roll < 22) used = TryInterlock(b, high, pos, rem, vlo, vhi);
                else if (d >= 5 && rem >= 14 && roll >= 22 && roll < 26) used = TryGallery(b, high, pos, rem, vlo, vhi);
                else if (social && d >= 4 && rem >= 16 && roll >= 26 && roll < 34) used = CubicleRun(b, high, pos, rem, vlo, vhi);
                if (used > 0)
                {
                    pos += used + 1; rem -= used + 1;
                    continue;
                }

                PIntent it = Pick(b.Arch, high);
                int w = it.Min + rng.Next(it.Max - it.Min + 1);
                if (w > rem) w = rem;
                int after = rem - w - 1;
                if (after < 3) { w = rem; after = 0; }
                RoomFunction f = it.F;
                bool[] keep = null;
                if (!rotundaDone[b.Bid] && w >= 9 && w <= 15 && d >= 8 && rng.Next(100) < 12)
                {
                    keep = MakeMask(KDisc, w, d);
                    if (keep != null) { f = RoomFunction.Auditorium; rotundaDone[b.Bid] = true; }
                }

                if (keep == null)
                {
                    int kind = ChooseKind(f, w, d);
                    if (kind != KRect) keep = MakeMask(kind, w, d);
                    if (keep != null && kind == KTrap && (b.Arch == "Office" || b.Arch == "Commons")) f = RoomFunction.Auditorium;
                }

                NewRoom(b, high, pos, pos + w - 1, vlo, vhi, f, true, keep, vlo, vhi);
                pos += w + 1;
                rem = after;
            }
        }

        int TrySuite(PBar b, bool high, int pos, int rem, int vlo, int vhi)
        {
            int d = vhi - vlo + 1;
            int ta = (d >= 8 && rng.Next(2) == 0) ? 3 : 2;
            if (d - ta - 1 < 3) return 0;
            int k = rng.Next(100) < 12 ? 1 : (rng.Next(2) == 0 ? 2 : 3);
            while (k > 1 && 3 * k + (k - 1) > rem) k--;
            int minW = 3 * k + (k - 1);
            if (minW > rem) return 0;
            int w = minW + rng.Next(Math.Min(5, rem - minW) + 1);
            int after = rem - w - 1;
            if (after > 0 && after < 3) w = rem;
            int a0, a1;
            VR(high, vlo, vhi, 0, ta - 1, out a0, out a1);
            RoomFunction af = (b.Arch == "Office" || b.Arch == "Commons") ? RoomFunction.Reception : RoomFunction.Closet;
            NewRoom(b, high, pos, pos + w - 1, a0, a1, af, true, null, vlo, vhi);
            int ai = rooms.Count - 1;
            int[] widths = SplitWidths(w - (k - 1), k, 3);
            int cur = pos;
            for (int i = 0; i < k; i++)
            {
                int i0, i1;
                VR(high, vlo, vhi, ta + 1, d - 1, out i0, out i1);
                PRoom inner = NewRoom(b, high, cur, cur + widths[i] - 1, i0, i1, PickSmall(b.Arch), true, null, vlo, vhi);
                inner.Parent = ai;
                cur += widths[i] + 1;
            }

            if (k >= 2) suiteCount++;
            return w;
        }

        // An operating room with its own scrub anteroom: the room is reached only through the vestibule.
        int TryOperating(PBar b, bool high, int pos, int rem, int vlo, int vhi)
        {
            int d = vhi - vlo + 1;
            int wo = 6 + rng.Next(3), vw = 3;
            int total = wo + 1 + vw;
            if (total > rem) return 0;
            int after = rem - total - 1;
            if (after > 0 && after < 3) { vw += after + 1; total = rem; }
            int v0, v1;
            VR(high, vlo, vhi, 0, d - 1, out v0, out v1);
            bool vestLeft = rng.Next(2) == 0;
            int vu0 = vestLeft ? pos : pos + wo + 1, ou0 = vestLeft ? pos + vw + 1 : pos;
            NewRoom(b, high, vu0, vu0 + vw - 1, v0, v1, RoomFunction.Closet, true, null, vlo, vhi);
            int vi = rooms.Count - 1;
            PRoom op = NewRoom(b, high, ou0, ou0 + wo - 1, v0, v1, RoomFunction.OperatingRoom, true, null, vlo, vhi);
            op.Parent = vi;
            return total;
        }

        int TryPair(PBar b, bool high, int pos, int rem, int vlo, int vhi)
        {
            int d = vhi - vlo + 1;
            int wl = 4 + rng.Next(3), wr = 4 + rng.Next(3), vw = 3;
            int total = wl + 1 + vw + 1 + wr;
            if (total > rem) return 0;
            int after = rem - total - 1;
            if (after > 0 && after < 3) { vw += after + 1; total = rem; }
            int v0, v1;
            VR(high, vlo, vhi, 0, d - 1, out v0, out v1);
            RoomFunction vf = (b.Arch == "Office" || b.Arch == "Commons") ? RoomFunction.Reception : RoomFunction.Closet;
            NewRoom(b, high, pos + wl + 1, pos + wl + vw, v0, v1, vf, true, null, vlo, vhi);
            int vi = rooms.Count - 1;
            PRoom left = NewRoom(b, high, pos, pos + wl - 1, v0, v1, PickSmall(b.Arch), true, null, vlo, vhi);
            left.Parent = vi;
            PRoom right = NewRoom(b, high, pos + wl + vw + 2, pos + total - 1, v0, v1, PickSmall(b.Arch), true, null, vlo, vhi);
            right.Parent = vi;
            suiteCount++;
            return total;
        }

        int TryInterlock(PBar b, bool high, int pos, int rem, int vlo, int vhi)
        {
            int d = vhi - vlo + 1;
            int wi = Math.Min(rem, 9 + rng.Next(4));
            if (wi < 9) return 0;
            int after = rem - wi - 1;
            if (after > 0 && after < 3) wi = rem;
            int cw = 3 + rng.Next(2), cd = 3 + rng.Next(2);
            if (d - cd - 1 < 2) cd = 3;
            if (d - cd - 1 < 2) return 0;
            bool right = rng.Next(2) == 0;
            bool[] keep = new bool[wi * d];
            for (int s = 0; s < wi; s++)
                for (int t = 0; t < d; t++)
                {
                    bool ring = right ? (s >= wi - cw - 1 && t >= d - cd - 1) : (s <= cw && t >= d - cd - 1);
                    keep[s * d + t] = !ring;
                }

            if (!ValidMask(keep, wi, d)) return 0;
            PIntent it = Pick(b.Arch, high);
            RoomFunction lf = (it.Max >= 8 && it.F != RoomFunction.Cafeteria) ? it.F : (b.Arch == "Office" ? RoomFunction.OpenOffice : PickSmall(b.Arch));
            NewRoom(b, high, pos, pos + wi - 1, vlo, vhi, lf, true, keep, vlo, vhi);
            int li = rooms.Count - 1;
            int c0, c1;
            VR(high, vlo, vhi, d - cd, d - 1, out c0, out c1);
            int su0 = right ? pos + wi - cw : pos, su1 = right ? pos + wi - 1 : pos + cw - 1;
            PRoom closet = NewRoom(b, high, su0, su1, c0, c1, PickSmall(b.Arch), true, null, vlo, vhi);
            closet.Parent = li;
            return wi;
        }

        int TryGallery(PBar b, bool high, int pos, int rem, int vlo, int vhi)
        {
            int d = vhi - vlo + 1;
            int w = Math.Min(rem, 14 + rng.Next(9));
            int after = rem - w - 1;
            if (after > 0 && after < 3) w = rem;
            if (w < 12) return 0;
            bool[] keep = MakeMask(KComb, w, d);
            if (keep == null) return 0;
            RoomFunction f = b.Arch == "Commons" ? RoomFunction.Cafeteria : (b.Arch == "Lab" ? RoomFunction.Lab : (b.Arch == "Office" ? RoomFunction.OpenOffice : RoomFunction.Archive));
            if (b.Arch == "Medical") f = RoomFunction.Ward; else if (b.Arch == "Barracks") f = RoomFunction.Bunkroom; else if (b.Arch == "Research") f = RoomFunction.Lab; else if (b.Arch == "Residential") f = RoomFunction.Cafeteria;
            NewRoom(b, high, pos, pos + w - 1, vlo, vhi, f, true, keep, vlo, vhi);
            return w;
        }

        int CubicleRun(PBar b, bool high, int pos, int rem, int vlo, int vhi)
        {
            int w0 = 3 + rng.Next(2);
            RoomFunction cf = b.Arch == "Lab" && rng.Next(2) == 0 ? RoomFunction.Lab : RoomFunction.PrivateOffice;
            if (b.Arch == "Medical") { w0 = 5 + rng.Next(2); cf = RoomFunction.Ward; }
            else if (b.Arch == "Residential") { w0 = 4 + rng.Next(2); cf = RoomFunction.Apartment; }
            else if (b.Arch == "Barracks") { w0 = 8 + rng.Next(2); cf = RoomFunction.Bunkroom; }
            else if (b.Arch == "Research") { w0 = 5 + rng.Next(2); cf = RoomFunction.Lab; }
            int n = Math.Min(rem / (w0 + 1), 4 + rng.Next(3));
            if (n < 3) return 0;
            int total = n * w0 + (n - 1);
            int after = rem - total - 1;
            int[] ws = new int[n];
            for (int i = 0; i < n; i++) ws[i] = w0;
            if (rng.Next(2) == 0) { ws[rng.Next(n)]++; total++; }
            if (after > 0 && after < 3 + 0) { int extra = rem - total; for (int i = 0; i < extra; i++) { ws[i % n]++; total++; } }
            if (total > rem) return 0;
            int cur = pos;
            for (int i = 0; i < n; i++)
            {
                NewRoom(b, high, cur, cur + ws[i] - 1, vlo, vhi, cf, true, null, vlo, vhi);
                cur += ws[i] + 1;
            }

            return total;
        }

        // ------------------------------------------------------------------ doors between a room and its parent (suites, interlocks)
        bool PlaceInnerDoor(int ri, int pi)
        {
            PRoom r = rooms[ri];
            int pl = pi + 1;
            int mx = (r.X0 + r.X1) / 2, my = (r.Y0 + r.Y1) / 2;
            int best = -1, bestScore = int.MaxValue;
            foreach (int ci in r.Cells)
            {
                int x = ci % W, y = ci / W;
                for (int d = 0; d < 4; d++)
                {
                    int cx = x + DX[d], cy = y + DY[d];
                    if (!InB(cx, cy) || tile[cy * W + cx] != PlanTile.Wall) continue;
                    if (Lab(cx + DX[d], cy + DY[d]) != pl) continue;
                    if (InnerDoorKind(cx, cy) == 0) continue;
                    int score = (Math.Abs(cx - mx) + Math.Abs(cy - my)) * 4 + rng.Next(3);
                    if (score < bestScore) { bestScore = score; best = cy * W + cx; }
                }
            }

            if (best < 0) return false;
            tile[best] = PlanTile.Door;
            return true;
        }

        // Keeps only the largest 4-connected piece of a freshly painted room.
        void PruneLeaves(PRoom r, int label)
        {
            if (r.Cells.Count < 12) return;
            for (int pass = 0; pass < 6; pass++)
            {
                List<int> keep = new List<int>();
                bool changed = false;
                foreach (int c in r.Cells)
                {
                    int cx = c % W, cy = c / W, n = 0;
                    for (int d = 0; d < 4; d++)
                    {
                        int nx = cx + DX[d], ny = cy + DY[d];
                        if (InB(nx, ny) && lab[ny * W + nx] == label) n++;
                    }

                    if (n < 2) { lab[c] = -1; changed = true; }
                    else keep.Add(c);
                }

                r.Cells = keep;
                if (!changed) break;
            }
        }

        void KeepLargestPiece(PRoom r, int label)
        {
            PruneLeaves(r, label);
            KeepLargestPieceInner(r, label);
            PruneLeaves(r, label);
        }

        void KeepLargestPieceInner(PRoom r, int label)
        {
            if (r.Cells.Count < 2) return;
            HashSet<int> left = new HashSet<int>(r.Cells);
            List<int> bestPiece = null;
            List<List<int>> pieces = new List<List<int>>();
            foreach (int c in r.Cells)
            {
                if (!left.Contains(c)) continue;
                List<int> piece = new List<int>();
                piece.Add(c); left.Remove(c);
                for (int h = 0; h < piece.Count; h++)
                {
                    int cc = piece[h]; int cx = cc % W, cy = cc / W;
                    for (int d = 0; d < 4; d++)
                    {
                        int nx = cx + DX[d], ny = cy + DY[d];
                        if (!InB(nx, ny)) continue;
                        int ni = ny * W + nx;
                        if (left.Contains(ni)) { left.Remove(ni); piece.Add(ni); }
                    }
                }

                pieces.Add(piece);
                if (bestPiece == null || piece.Count > bestPiece.Count) bestPiece = piece;
            }

            if (pieces.Count <= 1) return;
            foreach (List<int> piece in pieces)
                if (piece != bestPiece) foreach (int c in piece) lab[c] = -1;
            r.Cells = bestPiece;
        }
    }
}
