﻿using System.Linq;
using UnityEngine;
using System.Collections;
using System;
using System.Collections.Generic;
using Random = System.Random;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class PlanetGenerate : MonoBehaviour 
{
    Random globalRandom;
    public int SubdivisionLevel = 2;
    public float DistortionLevel = 0f;
    int plateCount = 36;
	// Use this for initialization
	void Start ()
    {

        //Polyhedra p = GenerateIcosahedron();
        //p.DebugDraw(5f, Color.red, 10000f);

        ////Polyhedra p2 = GenerateSubdividedIcosahedron(2);
        ////p2.DebugDraw(10f, Color.green, 10000f);
        globalRandom = new Random(4);
        Polyhedra p10 = GenerateSubdividedIcosahedron(20);
        //p10.DebugDraw(1000f, Color.yellow, 10000f);


        Polyhedra planet = GeneratePlanetMesh(SubdivisionLevel, DistortionLevel);
        //planet.DebugDraw(20f, Color.green, 10000f);
        planet.generatePlanetTopology();
        //planet.DebugDraw(40f, Color.red, 10000f);
        //planet.topology.DebugDraw(1f, Color.red, 10000f);
        Debug.Log(planet.topology.tiles.Count);
	    var verts = new List<Vector3>();
        var norms = new List<Vector3>();
        var tris = new List<int>();

	    var tileStart = 0;
	    foreach (var tile in planet.topology.tiles)
	    {
            if(tileStart >= 64000)
            {
                GameObject go = new GameObject("child");
                go.transform.parent = this.gameObject.transform;

                var mesh = new Mesh { name = "World Mesh" };
                go.AddComponent<MeshFilter>().mesh = mesh;
                var mr = go.AddComponent<MeshRenderer>();
                mr.material = new Material(Shader.Find("Diffuse"));
                mesh.vertices = verts.ToArray();
                //mesh.uv = UV;
                mesh.triangles = tris.ToArray();
                mesh.normals = norms.ToArray();

                tileStart = 0;
                verts.Clear();
                norms.Clear();
                tris.Clear();
            }
            verts.Add(tile.averagePosition);
            norms.Add(tile.averagePosition.normalized);
	        tileStart = verts.Count - 1;
	        foreach (var corner in tile.corners)
	        {
	            verts.Add(corner.position);
                norms.Add(tile.averagePosition.normalized);

                ////brnDEBUG
                //var cubeMark = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                //cubeMark.transform.position = corner.position;
                //cubeMark.renderer.material.color = new Color(255, 0, 0);
	        }

            for (int i = 1; i <= tile.corners.Length; i++)
            {
                var mid = (i + 1) % (tile.corners.Length + 1) != 0
                    ? i + 1
                    : 1;

                tris.Add(tileStart);
                tris.Add(tileStart + mid);
                tris.Add(tileStart + i);

            }
	    }

        GameObject ch = new GameObject("child");
        ch.transform.parent = this.gameObject.transform;
        var mesh2 = new Mesh { name = "World Mesh" };
        ch.AddComponent<MeshFilter>().mesh = mesh2;
        var mr2 = ch.AddComponent<MeshRenderer>();
        mr2.material = new Material(Shader.Find("Diffuse"));
        mesh2.vertices = verts.ToArray();
        //mesh.uv = UV;
        mesh2.triangles = tris.ToArray();
        mesh2.normals = norms.ToArray();

        //GameObject go = new GameObject("icosahedron");
        //MeshFilter mf = go.AddComponent<MeshFilter>();
        //MeshRenderer mr = go.AddComponent<MeshRenderer>();


        //mf.mesh = GenerateIcosahedron();
        //mr.material = new Material(Shader.Find("Diffuse"));

        //go.transform.position = Vector3.zero;
	}
	
    Polyhedra GeneratePlanetMesh(int icosahedronSubdivision, float topologyDistortionRate)
    {
        var mesh = GenerateSubdividedIcosahedron(icosahedronSubdivision);
        var totalDistortion = Math.Ceiling(mesh.edges.Count * topologyDistortionRate);
		var remainingIterations = 1;
        int i;
        while(remainingIterations > 0)
        {
			var iterationDistortion = (int)Math.Floor(totalDistortion / remainingIterations);
			totalDistortion -= iterationDistortion;
			DistortMesh(mesh, iterationDistortion, globalRandom);
			RelaxMesh(mesh, 0.5f);
			--remainingIterations;
        }
        
        //var initialIntervalIteration = action.intervalIteration;
	
		var averageNodeRadius = Math.Sqrt(4 * Math.PI / mesh.nodes.Count);
		var minShiftDelta = averageNodeRadius / 50000 * mesh.nodes.Count;
		var maxShiftDelta = averageNodeRadius / 50 * mesh.nodes.Count;

		float priorShift, shiftDelta;
		var currentShift = RelaxMesh(mesh, 0.5f);

        do
        {
            priorShift = currentShift;
            currentShift = RelaxMesh(mesh, 0.5f);
            shiftDelta = Math.Abs(currentShift - priorShift);
        } while (shiftDelta >= minShiftDelta /* && action.intervalIteration - initialIntervalIteration < 300*/);

        for(i = 0; i < mesh.faces.Count; ++i)
        {
            var face = mesh.faces[i];
            var p0 = mesh.nodes[face.n[0]].p;
            var p1 = mesh.nodes[face.n[1]].p;
            var p2 = mesh.nodes[face.n[2]].p;
            face.centroid = CalculateFaceCentroid(p0, p1, p2).normalized;
        }

        for(i = 0; i < mesh.nodes.Count; ++i)
        {
            var node = mesh.nodes[i];
            var faceIndex = node.f[0];
            for(var j = 1; j < node.f.Count - 1; ++j)
            {
                faceIndex = FindNextFaceIndex(mesh, i, faceIndex);
                var k = node.f.IndexOf(faceIndex);
                node.f[k] = node.f[j];
                node.f[j] = faceIndex;
            }
        }

        return mesh;
    }

    Polyhedra GenerateSubdividedIcosahedron(int degree)
    {
        var icosahedron = GenerateIcosahedron();

        List<Node> nodes = new List<Node>();
        for(int i = 0; i < icosahedron.nodes.Count; ++i)
        {
            nodes.Add(new Node(icosahedron.nodes[i].p));
        }

        List<Edge> edges = new List<Edge>();
        for (var i = 0; i < icosahedron.edges.Count; ++i)
        {
            var edge = icosahedron.edges[i];
            //edge.subdivided_n = new List<int>();
            //edge.subdivided_e = [];
            var n0 = icosahedron.nodes[edge.n[0]];
            var n1 = icosahedron.nodes[edge.n[1]];
            var p0 = n0.p;
            var p1 = n1.p;
            var delta = p1 - p0; //p1.clone().sub(p0);
            nodes[edge.n[0]].e.Add(edges.Count);
            var priorNodeIndex = edge.n[0];
            for (var s = 1; s < degree; ++s)
            {
                var edgeIndex = edges.Count;
                var nodeIndex = nodes.Count;
                edge.subdivided_e.Add(edgeIndex);
                edge.subdivided_n.Add(nodeIndex);
                edges.Add(new Edge(priorNodeIndex, nodeIndex)); // { n: [ priorNodeIndex, nodeIndex ], f: [] });
                priorNodeIndex = nodeIndex;
                Node newnode = new Node(Vector3.Slerp(p0, p1, (float)s / degree));
                newnode.e = new List<int>() { edgeIndex, edgeIndex + 1 };
                nodes.Add(newnode); // { p: slerp(p0, p1, s / degree), e: [ edgeIndex, edgeIndex + 1 ], f: [] });
            }
            edge.subdivided_e.Add(edges.Count);
            nodes[edge.n[1]].e.Add(edges.Count);
            edges.Add(new Edge(priorNodeIndex, edge.n[1])); // { n: [ priorNodeIndex, edge.n[1] ], f: [] });
        }

        List<Face> faces = new List<Face>();
        for (var i = 0; i < icosahedron.faces.Count; ++i)
        {
            var face = icosahedron.faces[i];
            var edge0 = icosahedron.edges[face.e[0]];
            var edge1 = icosahedron.edges[face.e[1]];
            var edge2 = icosahedron.edges[face.e[2]];
            var point0 = icosahedron.nodes[face.n[0]].p;
            var point1 = icosahedron.nodes[face.n[1]].p;
            var point2 = icosahedron.nodes[face.n[2]].p;
            var delta = point1 - point0; // point1.clone().sub(point0);


            var getEdgeNode0 = (face.n[0] == edge0.n[0])
                ? new Func<int, int>((k) => { return edge0.subdivided_n[k]; })
                : new Func<int, int>((k) => { return edge0.subdivided_n[degree - 2 - k]; });
            var getEdgeNode1 = (face.n[1] == edge1.n[0])
                ? new Func<int, int>((k) => { return edge1.subdivided_n[k]; })
                : new Func<int, int>((k) => { return edge1.subdivided_n[degree - 2 - k]; });
            var getEdgeNode2 = (face.n[0] == edge2.n[0])
                ? new Func<int, int>((k) => { return edge2.subdivided_n[k]; })
                : new Func<int, int>((k) => { return edge2.subdivided_n[degree - 2 - k]; });

            var faceNodes = new List<int>();
            faceNodes.Add(face.n[0]);
            for (var j = 0; j < edge0.subdivided_n.Count; ++j)
                faceNodes.Add(getEdgeNode0(j));
            faceNodes.Add(face.n[1]);
            for (var s = 1; s < degree; ++s)
            {
                faceNodes.Add(getEdgeNode2(s - 1));
                var p0 = nodes[getEdgeNode2(s - 1)].p;
                var p1 = nodes[getEdgeNode1(s - 1)].p;
                for (var t = 1; t < degree - s; ++t)
                {
                    faceNodes.Add(nodes.Count);
                    nodes.Add(new Node(Vector3.Slerp(p0, p1, (float)t / (degree - s)))); // { p: slerp(p0, p1, t / (degree - s)), e: [], f: [], });
                }
                faceNodes.Add(getEdgeNode1(s - 1));
            }
            faceNodes.Add(face.n[2]);

            var getEdgeEdge0 = (face.n[0] == edge0.n[0])
                ? new Func<int, int>((k) => { return edge0.subdivided_e[k]; })
                : new Func<int, int>((k) => { return edge0.subdivided_e[degree - 1 - k]; });
            var getEdgeEdge1 = (face.n[1] == edge1.n[0])
                ? new Func<int, int>((k) => { return edge1.subdivided_e[k]; })
                : new Func<int, int>((k) => { return edge1.subdivided_e[degree - 1 - k]; });
            var getEdgeEdge2 = (face.n[0] == edge2.n[0])
                ? new Func<int, int>((k) => { return edge2.subdivided_e[k]; })
                : new Func<int, int>((k) => { return edge2.subdivided_e[degree - 1 - k]; });

            var faceEdges0 = new List<int>();
            for (var j = 0; j < degree; ++j)
                faceEdges0.Add(getEdgeEdge0(j));
            var nodeIndex = degree + 1;
            for (var s = 1; s < degree; ++s)
            {
                for (var t = 0; t < degree - s; ++t)
                {
                    faceEdges0.Add(edges.Count);
                    var edge = new Edge(faceNodes[nodeIndex], faceNodes[nodeIndex + 1]);
                    nodes[edge.n[0]].e.Add(edges.Count);
                    nodes[edge.n[1]].e.Add(edges.Count);
                    edges.Add(edge);
                    ++nodeIndex;
                }
                ++nodeIndex;
            }

            var faceEdges1 = new List<int>();

            nodeIndex = 1;
            for (var s = 0; s < degree; ++s)
            {
                for (var t = 1; t < degree - s; ++t)
                {
                    faceEdges1.Add(edges.Count);
                    var edge = new Edge(faceNodes[nodeIndex], faceNodes[nodeIndex + degree - s]);
                    nodes[edge.n[0]].e.Add(edges.Count);
                    nodes[edge.n[1]].e.Add(edges.Count);
                    edges.Add(edge);
                    ++nodeIndex;
                }
                faceEdges1.Add(getEdgeEdge1(s));
                nodeIndex += 2;
            }

            var faceEdges2 = new List<int>();
            nodeIndex = 1;
            for (var s = 0; s < degree; ++s)
            {
                faceEdges2.Add(getEdgeEdge2(s));
                for (var t = 1; t < degree - s; ++t)
                {
                    faceEdges2.Add(edges.Count);
                    var edge = new Edge(faceNodes[nodeIndex], faceNodes[nodeIndex + degree - s + 1]);
                    nodes[edge.n[0]].e.Add(edges.Count);
                    nodes[edge.n[1]].e.Add(edges.Count);
                    edges.Add(edge);
                    ++nodeIndex;
                }
                nodeIndex += 2;
            }

            nodeIndex = 0;
            var edgeIndex = 0;
            for (var s = 0; s < degree; ++s)
            {
                for (var t = 1; t < degree - s + 1; ++t)
                {
                    var subFace = new Face(new int[] { faceNodes[nodeIndex], faceNodes[nodeIndex + 1], faceNodes[nodeIndex + degree - s + 1] }
                        , new int[] { faceEdges0[edgeIndex], faceEdges1[edgeIndex], faceEdges2[edgeIndex] });
                    //{n: [ faceNodes[nodeIndex], faceNodes[nodeIndex + 1], faceNodes[nodeIndex + degree - s + 1], ],
                    //e: [ faceEdges0[edgeIndex], faceEdges1[edgeIndex], faceEdges2[edgeIndex], ], };
                    nodes[subFace.n[0]].f.Add(faces.Count);
                    nodes[subFace.n[1]].f.Add(faces.Count);
                    nodes[subFace.n[2]].f.Add(faces.Count);
                    edges[subFace.e[0]].f.Add(faces.Count);
                    edges[subFace.e[1]].f.Add(faces.Count);
                    edges[subFace.e[2]].f.Add(faces.Count);
                    faces.Add(subFace);
                    ++nodeIndex;
                    ++edgeIndex;
                }
                ++nodeIndex;
            }

            nodeIndex = 1;
            edgeIndex = 0;
            for (var s = 1; s < degree; ++s)
            {
                for (var t = 1; t < degree - s + 1; ++t)
                {
                    var subFace = new Face(new int[] { faceNodes[nodeIndex], faceNodes[nodeIndex + degree - s + 2], faceNodes[nodeIndex + degree - s + 1] }
                                          , new int[] { faceEdges2[edgeIndex + 1], faceEdges0[edgeIndex + degree - s + 1], faceEdges1[edgeIndex] });
                    //n: [ faceNodes[nodeIndex], faceNodes[nodeIndex + degree - s + 2], faceNodes[nodeIndex + degree - s + 1], ],
                    //e: [ faceEdges2[edgeIndex + 1], faceEdges0[edgeIndex + degree - s + 1], faceEdges1[edgeIndex], ], };
                    nodes[subFace.n[0]].f.Add(faces.Count);
                    nodes[subFace.n[1]].f.Add(faces.Count);
                    nodes[subFace.n[2]].f.Add(faces.Count);
                    edges[subFace.e[0]].f.Add(faces.Count);
                    edges[subFace.e[1]].f.Add(faces.Count);
                    edges[subFace.e[2]].f.Add(faces.Count);
                    faces.Add(subFace);
                    ++nodeIndex;
                    ++edgeIndex;
                }
                nodeIndex += 2;
                edgeIndex += 1;
            }
        }
        return new Polyhedra(nodes, edges, faces);
    }

    Polyhedra GenerateIcosahedron()
    {
        var phi = (float) ((1.0 + Math.Sqrt(5.0)) / 2.0);
        float du =(float) (1.0 / Math.Sqrt(phi * phi + 1.0));
        float dv =(float) (phi * du);

        //Vector3[] vertices = new Vector3[] { new Vector3(0, +dv, +du),
        //                                     new Vector3(0, +dv, -du),
        //                                     new Vector3(0, -dv, +du),
        //                                     new Vector3(0, -dv, -du),
        //                                     new Vector3(+du, 0, +dv),
        //                                     new Vector3(-du, 0, +dv),
        //                                     new Vector3(+du, 0, -dv),
        //                                     new Vector3(-du, 0, -dv),
        //                                     new Vector3(+dv, +du, 0),
        //                                     new Vector3(+dv, -du, 0),
        //                                     new Vector3(-dv, +du, 0),
        //                                     new Vector3(-dv, -du, 0)};
        //int[] tris = new int[] { 0, 1, 8,
        //                         0, 4, 5,
        //                         0, 5, 10,
        //                         0, 8, 4,
        //                         0, 10, 1,
        //                         1, 6, 8,
        //                         1, 7, 6,
        //                         1, 10, 7,
        //                         2, 3, 11,
        //                         2, 4, 9,
        //                         2, 5, 4,
        //                         2, 9, 3,
        //                         2, 11, 5,
        //                         3, 6, 7,
        //                         3, 7, 11,
        //                         3, 9, 6,
        //                         4, 8, 9,
        //                         5, 11, 10,
        //                         6, 9, 8,
        //                         7, 10, 11};
        //Mesh icosahedron = new Mesh();
        //icosahedron.vertices = vertices;
        //icosahedron.SetTriangles(tris, 0);
        //icosahedron.RecalculateNormals();
        //icosahedron.RecalculateBounds();

        var nodes = new List<Node>() {
            new Node(new Vector3(0, +dv, +du)),
            new Node(new Vector3(0, +dv, -du)),
            new Node(new Vector3(0, -dv, +du)),
            new Node(new Vector3(0, -dv, -du)),
            new Node(new Vector3(+du, 0, +dv)),
            new Node(new Vector3(-du, 0, +dv)),
            new Node(new Vector3(+du, 0, -dv)),
            new Node(new Vector3(-du, 0, -dv)),
            new Node(new Vector3(+dv, +du, 0)),
            new Node(new Vector3(+dv, -du, 0)),
            new Node(new Vector3(-dv, +du, 0)),
            new Node(new Vector3(-dv, -du, 0))};

        var edges = new List<Edge>() {
            new Edge(0,  1),
            new Edge(0,  4),
            new Edge(0,  5),
            new Edge(0,  8),
            new Edge(0, 10),
            new Edge(1,  6),
            new Edge(1,  7),
            new Edge(1,  8),
            new Edge(1, 10),
            new Edge(2,  3),
            new Edge(2,  4),
            new Edge(2,  5),
            new Edge(2,  9),
            new Edge(2, 11),
            new Edge(3,  6),
            new Edge(3,  7),
            new Edge(3,  9),
            new Edge(3, 11),
            new Edge(4,  5),
            new Edge(4,  8),
            new Edge(4,  9),
            new Edge(5, 10),
            new Edge(5, 11),
            new Edge(6,  7),
            new Edge(6,  8),
            new Edge(6,  9),
            new Edge(7, 10),
            new Edge(7, 11),
            new Edge(8,  9),
            new Edge(10, 11)};

        var faces = new List<Face>() {
            new Face(new int[] {0,  1,  8 },new int[] { 0,  7,  3 }),
            new Face(new int[] {0,  4,  5 },new int[] { 1, 18,  2 }),
            new Face(new int[] {0,  5, 10 },new int[] { 2, 21,  4 }),
            new Face(new int[] {0,  8,  4 },new int[] { 3, 19,  1 }),
            new Face(new int[] {0, 10,  1 },new int[] { 4,  8,  0 }),
            new Face(new int[] {1,  6,  8 },new int[] { 5, 24,  7 }),
            new Face(new int[] {1,  7,  6 },new int[] { 6, 23,  5 }),
            new Face(new int[] {1, 10,  7 },new int[] { 8, 26,  6 }),
            new Face(new int[] {2,  3, 11 },new int[] { 9, 17, 13 }),
            new Face(new int[] {2,  4,  9 },new int[] {10, 20, 12 }),
            new Face(new int[] {2,  5,  4 },new int[] {11, 18, 10 }),
            new Face(new int[] {2,  9,  3 },new int[] {12, 16,  9 }),
            new Face(new int[] {2, 11,  5 },new int[] {13, 22, 11 }),
            new Face(new int[] {3,  6,  7 },new int[] {14, 23, 15 }),
            new Face(new int[] {3,  7, 11 },new int[] {15, 27, 17 }),
            new Face(new int[] {3,  9,  6 },new int[] {16, 25, 14 }),
            new Face(new int[] {4,  8,  9 },new int[] {19, 28, 20 }),
            new Face(new int[] {5, 11, 10 },new int[] {22, 29, 21 }),
            new Face(new int[] {6,  9,  8 },new int[] {25, 28, 24 }),
            new Face(new int[] {7, 10, 11 },new int[] {26, 29, 27 })
        };

        for (var i = 0; i < edges.Count; ++i)
            for (var j = 0; j < edges[i].n.Length; ++j)
                nodes[j].e.Add(i);

        for (var i = 0; i < faces.Count; ++i)
            for (var j = 0; j < faces[i].n.Count; ++j)
                nodes[j].f.Add(i);

        for (var i = 0; i < faces.Count; ++i)
            for (var j = 0; j < faces[i].e.Count; ++j)
                edges[j].f.Add(i);


        return new Polyhedra(nodes, edges, faces);
    }

    bool DistortMesh(Polyhedra mesh, int degree, Random random)
    {
        double totalSurfaceArea = 4 * Math.PI;
        double idealFaceArea = totalSurfaceArea / mesh.faces.Count;
        double idealEdgeLength = Math.Sqrt(idealFaceArea * 4 / Math.Sqrt(3));
        double idealFaceHeight = idealEdgeLength * Math.Sqrt(3) / 2;

        var rotationPredicate = new Func<Node, Node, Node, Node, bool>((oldNode0, oldNode1, newNode0, newNode1) =>
        {
            if (newNode0.f.Count >= 7 ||
                newNode1.f.Count >= 7 ||
                oldNode0.f.Count <= 5 ||
                oldNode1.f.Count <= 5) return false;

            var oldEdgeLength = Vector3.Distance(oldNode0.p, oldNode1.p);
            var newEdgeLength = Vector3.Distance(newNode0.p, newNode1.p);
            var ratio = oldEdgeLength / newEdgeLength;
            if (ratio >= 2 || ratio <= 0.5) return false;
            var v0 = (oldNode1.p - oldNode0.p) / oldEdgeLength;
            var v1 = (newNode0.p - oldNode0.p).normalized;
            var v2 = (newNode1.p - oldNode0.p).normalized;
            if (Vector3.Dot(v0, v1) < 0.2 || Vector3.Dot(v0, v2) < 0.2) return false;
            v0 *= -1;
            var v3 = (newNode0.p - oldNode1.p).normalized;
            var v4 = (newNode1.p - oldNode1.p).normalized;
            if (Vector3.Dot(v0, v3) < 0.2 || Vector3.Dot(v0, v4) < 0.2) return false;

            return true;
        });

        var i = 0;

        while(i < degree)
        {
            var consecutiveFailedAttempts = 0;
            var edgeIndex = random.Next(0, mesh.edges.Count);
            while(!ConditionalRotateEdge(mesh, edgeIndex, rotationPredicate))
            {
                if (++consecutiveFailedAttempts >= mesh.edges.Count) return false;
                edgeIndex = (edgeIndex + 1) % mesh.edges.Count;
            }
            ++i;
        }
        return true;
    }

    float RelaxMesh(Polyhedra mesh, float multiplier)
    {
        var totalSurfaceArea = 4 * Math.PI;
        var idealFaceArea = totalSurfaceArea / mesh.faces.Count;
        var idealEdgeLength = Math.Sqrt(idealFaceArea * 4 / Math.Sqrt(3));
        var idealDistanceToCentroid = idealEdgeLength * Math.Sqrt(3) / 3 * 0.9;

        var pointShifts = new List<Vector3>(mesh.nodes.Count);
        int i = 0;
        for (i = 0; i < mesh.nodes.Count; i++)
        {
            pointShifts.Add(new Vector3(0, 0, 0));
        }

        i = 0;
        while(i < mesh.faces.Count)
        {
            var face = mesh.faces[i];
            var n0 = mesh.nodes[face.n[0]];
            var n1 = mesh.nodes[face.n[1]];
            var n2 = mesh.nodes[face.n[2]];
            var p0 = n0.p;
            var p1 = n1.p;
            var p2 = n2.p;
            var e0 = Vector3.Distance(p1, p0) / idealEdgeLength; // p1.distanceTo(p0) / idealEdgeLength;
            var e1 = Vector3.Distance(p2, p1) / idealEdgeLength; // p2.distanceTo(p1) / idealEdgeLength;
            var e2 = Vector3.Distance(p0, p2) / idealEdgeLength; // p0.distanceTo(p2) / idealEdgeLength;
            var centroid = CalculateFaceCentroid(p0, p1, p2).normalized;
            var v0 = centroid - p0; // centroid.clone().sub(p0);
            var v1 = centroid - p1;
            var v2 = centroid - p2;
            var length0 = v0.magnitude;
            var length1 = v1.magnitude;
            var length2 = v2.magnitude;
            v0 *= (float)(multiplier * (length0 - idealDistanceToCentroid) / length0);
            v1 *= (float)(multiplier * (length1 - idealDistanceToCentroid) / length1);
            v2 *= (float)(multiplier * (length2 - idealDistanceToCentroid) / length2);
            pointShifts[face.n[0]] += (v0);
            pointShifts[face.n[1]] += (v1);
            pointShifts[face.n[2]] += (v2);

            ++i;            
        }

        var origin = new Vector3(0, 0, 0);
        for (i = 0; i < mesh.nodes.Count; ++i)
        {
            pointShifts[i] = (mesh.nodes[i].p + (ProjectPointOnPlane(mesh.nodes[i].p, origin, pointShifts[i]))).normalized;
        }

        var rotationSupressions = new List<float>();
        for(i = 0; i < mesh.nodes.Count; ++i)
        {
            rotationSupressions.Add(0);
        }

        i = 0;
        while(i<mesh.edges.Count)
        {
            var edge = mesh.edges[i];
            var oldPoint0 = mesh.nodes[edge.n[0]].p;
            var oldPoint1 = mesh.nodes[edge.n[1]].p;
            var newPoint0 = pointShifts[edge.n[0]];
            var newPoint1 = pointShifts[edge.n[1]];
            var oldVector = (oldPoint1 - oldPoint0).normalized;
            var newVector = (newPoint1 - newPoint0).normalized;
            var suppression = (1 - Vector3.Dot(oldVector, newVector)) * 0.5;
            rotationSupressions[edge.n[0]] = Math.Max(rotationSupressions[edge.n[0]], (float)suppression);
            rotationSupressions[edge.n[1]] = Math.Max(rotationSupressions[edge.n[1]], (float)suppression);

            ++i;
        }

        float totalShift = 0;
        for(i = 0; i < mesh.nodes.Count; ++i)
        {
            var node = mesh.nodes[i];
            var point = node.p;
            var delta = point;
            node.p = Vector3.Lerp(point, pointShifts[i], 1f - (float)Math.Sqrt(rotationSupressions[i])).normalized;
            delta -= node.p;
            totalShift += delta.magnitude;
        }

        return totalShift;
    }

    Vector3 CalculateFaceCentroid(Vector3 pa, Vector3 pb, Vector3 pc)
    {
        var vabHalf = (pb - pa) / 2;
        var pabHalf = pa + vabHalf;
        var centroid = ((pc - pabHalf) / 3) + pabHalf;
        return centroid;
    }

    //This function returns a point which is a projection from a point to a plane.
    public static Vector3 ProjectPointOnPlane(Vector3 planeNormal, Vector3 planePoint, Vector3 point)
    {

        float distance;
        Vector3 translationVector;

        //First calculate the distance from the point to the plane:
        distance = SignedDistancePlanePoint(planeNormal, planePoint, point);

        //Reverse the sign of the distance
        distance *= -1;

        //Get a translation vector
        translationVector = SetVectorLength(planeNormal, distance);

        //Translate the point to form a projection
        return point + translationVector;
    }

    //create a vector of direction "vector" with length "size"
    public static Vector3 SetVectorLength(Vector3 vector, float size)
    {

        //normalize the vector
        Vector3 vectorNormalized = Vector3.Normalize(vector);

        //scale the vector
        return vectorNormalized *= size;
	}

    public static float CalculateTriangleArea(Vector3 pa, Vector3 pb, Vector3 pc)
    {
        float A = Vector3.Distance(pa, pb);
        float B = Vector3.Distance(pb, pc);
        float C = Vector3.Distance(pc, pa);

        float s = (A + B + C) / 2;
        float perimeter = A + B + C;
        float area = (float) Math.Sqrt(s * (s - A) * (s - B) * (s - C));
        return area;
    }

    //Get the shortest distance between a point and a plane. The output is signed so it holds information
	//as to which side of the plane normal the point is.
	public static float SignedDistancePlanePoint(Vector3 planeNormal, Vector3 planePoint, Vector3 point){
 
		return Vector3.Dot(planeNormal, (point - planePoint));
	}

    bool ConditionalRotateEdge(Polyhedra mesh, int edgeIndex, Func<Node, Node, Node, Node, bool> predicate)
    {
        var edge = mesh.edges[edgeIndex];
        var face0 = mesh.faces[edge.f[0]];
        var face1 = mesh.faces[edge.f[1]];
        var farNodeFaceIndex0 = GetFaceOppositeNodeIndex(face0, edge);
        var farNodeFaceIndex1 = GetFaceOppositeNodeIndex(face1, edge);
        var newNodeIndex0 = face0.n[farNodeFaceIndex0];
        var oldNodeIndex0 = face0.n[(farNodeFaceIndex0 + 1) % 3];
        var newNodeIndex1 = face1.n[farNodeFaceIndex1];
        var oldNodeIndex1 = face1.n[(farNodeFaceIndex1 + 1) % 3];
        var oldNode0 = mesh.nodes[oldNodeIndex0];
        var oldNode1 = mesh.nodes[oldNodeIndex1];
        var newNode0 = mesh.nodes[newNodeIndex0];
        var newNode1 = mesh.nodes[newNodeIndex1];
        var newEdgeIndex0 = face1.e[(farNodeFaceIndex1 + 2) % 3];
        var newEdgeIndex1 = face0.e[(farNodeFaceIndex0 + 2) % 3];
        var newEdge0 = mesh.edges[newEdgeIndex0];
        var newEdge1 = mesh.edges[newEdgeIndex1];

        if (!predicate(oldNode0, oldNode1, newNode0, newNode1)) return false;

        oldNode0.e.RemoveAt(oldNode0.e.IndexOf(edgeIndex));
        oldNode1.e.RemoveAt(oldNode1.e.IndexOf(edgeIndex));
        newNode0.e.Add(edgeIndex);
        newNode1.e.Add(edgeIndex);

        edge.n[0] = newNodeIndex0;
        edge.n[1] = newNodeIndex1;

        newEdge0.f.RemoveAt(newEdge0.f.IndexOf(edge.f[1]));
        newEdge1.f.RemoveAt(newEdge1.f.IndexOf(edge.f[0]));
        newEdge0.f.Add(edge.f[0]);
        newEdge1.f.Add(edge.f[1]);

        oldNode0.f.RemoveAt(oldNode0.f.IndexOf(edge.f[1]));
        oldNode1.f.RemoveAt(oldNode1.f.IndexOf(edge.f[0]));
        newNode0.f.Add(edge.f[1]);
        newNode1.f.Add(edge.f[0]);

        face0.n[(farNodeFaceIndex0 + 2) % 3] = newNodeIndex1;
        face1.n[(farNodeFaceIndex1 + 2) % 3] = newNodeIndex0;

        face0.e[(farNodeFaceIndex0 + 1) % 3] = newEdgeIndex0;
        face1.e[(farNodeFaceIndex1 + 1) % 3] = newEdgeIndex1;
        face0.e[(farNodeFaceIndex0 + 2) % 3] = edgeIndex;
        face1.e[(farNodeFaceIndex1 + 2) % 3] = edgeIndex;

        return true;
    }

    int GetFaceOppositeNodeIndex(Face face, Edge edge)
    {
        if (face.n[0] != edge.n[0] && face.n[0] != edge.n[1]) return 0;
        if (face.n[1] != edge.n[0] && face.n[1] != edge.n[1]) return 1;
        if (face.n[2] != edge.n[0] && face.n[2] != edge.n[1]) return 2;
        else return -1;
    }

    int GetEdgeOppositeFaceIndex(Edge edge, int faceIndex)
    {
        if (edge.f[0] == faceIndex) return edge.f[1];
        if (edge.f[1] == faceIndex) return edge.f[0];
        else return -1;
    }

    int FindNextFaceIndex(Polyhedra mesh, int nodeIndex, int faceIndex)
    {
        var node = mesh.nodes[nodeIndex];
        var face = mesh.faces[faceIndex];
        var nodeFaceIndex = face.n.IndexOf(nodeIndex);
        var edge = mesh.edges[face.e[(nodeFaceIndex + 2) % 3]];
        return GetEdgeOppositeFaceIndex(edge, faceIndex);
    }
}