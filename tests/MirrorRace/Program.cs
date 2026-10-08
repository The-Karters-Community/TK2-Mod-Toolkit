using System.Numerics;
using TK2.Customization;

int checks = 0;
void Check(bool condition, string message) { checks++; if (!condition) throw new Exception(message); }
void Reject(Action action, string message) { bool rejected = false; try { action(); } catch (ArgumentException) { rejected = true; } catch (NotSupportedException) { rejected = true; } Check(rejected, message); }
Vector3 Reflect(Vector3 v) => new(-v.X, v.Y, v.Z);
var random = new Random(6418);
for (int i = 0; i < 1000; i++)
{
    var q = Quaternion.Normalize(new Quaternion((float)random.NextDouble(), (float)random.NextDouble(), (float)random.NextDouble(), (float)random.NextDouble()));
    var result = MirrorGeometry.Rotation(q.X, q.Y, q.Z, q.W);
    var mirrored = new Quaternion(result.X, result.Y, result.Z, result.W);
    var v = new Vector3((float)random.NextDouble() * 100, (float)random.NextDouble() * 100, (float)random.NextDouble() * 100);
    Check(Vector3.Distance(Vector3.Transform(Reflect(v), mirrored), Reflect(Vector3.Transform(v, q))) < .0001f, "Reflected proper rotation and local geometry must equal reflection of original world geometry.");
    Check(Math.Abs(mirrored.LengthSquared() - 1) < .00001f, "Reflection preserves a unit quaternion.");
    var inverse = MirrorGeometry.Rotation(result.X, result.Y, result.Z, result.W);
    Check(inverse.X == q.X && inverse.Y == q.Y && inverse.Z == q.Z && inverse.W == q.W, "Rotation reflection is involutive.");
}

var stream = Enumerable.Repeat((byte)0x42, 64).ToArray();
BitConverter.GetBytes(12.5f).CopyTo(stream, 4); BitConverter.GetBytes(-6.25f).CopyTo(stream, 36);
byte[] original = (byte[])stream.Clone();
MirrorGeometry.NegateChannel(stream, 2, 32, 4, 0);
Check(BitConverter.ToSingle(stream, 4) == -12.5f && BitConverter.ToSingle(stream, 36) == 6.25f, "Negate strided positions.");
Check(stream.Where((b, i) => i != 7 && i != 39).SequenceEqual(original.Where((b, i) => i != 7 && i != 39)), "Do not corrupt UV, normal or packed color channels.");
MirrorGeometry.NegateChannel(stream, 2, 32, 4, 0); Check(stream.SequenceEqual(original), "Float byte reflection roundtrip.");

var half = BitConverter.GetBytes((ushort)0x3c00); MirrorGeometry.NegateChannel(half, 1, 2, 0, 1);
Check(BitConverter.ToUInt16(half) == 0xbc00, "Half precision sign bit.");
var snorm = new byte[] { 1, 127, 128, 255 }; MirrorGeometry.NegateChannel(snorm, 4, 1, 0, 3);
Check(snorm.SequenceEqual(new byte[] { 255, 129, 127, 1 }), "Signed normalized bytes and saturated -1.");
var snorm16 = BitConverter.GetBytes(short.MinValue); MirrorGeometry.NegateChannel(snorm16, 1, 2, 0, 5);
Check(BitConverter.ToInt16(snorm16) == short.MaxValue, "Signed normalized shorts saturate -1.");
Reject(() => MirrorGeometry.NegateChannel(BitConverter.GetBytes(float.NaN), 1, 4, 0, 0), "Reject NaN positions.");
Reject(() => MirrorGeometry.NegateChannel(BitConverter.GetBytes((ushort)0x7c00), 1, 2, 0, 1), "Reject infinite halves.");
Reject(() => MirrorGeometry.NegateChannel(new byte[12], 2, 8, 0, 0), "Reject incomplete vertex stream.");
Reject(() => MirrorGeometry.NegateChannel(new byte[8], 1, 8, 7, 0), "Reject attribute crossing vertex stride.");
Reject(() => MirrorGeometry.NegateChannel(new byte[8], 1, 8, 0, 2), "Reject unsupported unsigned packed position format.");

foreach (int indexSize in new[] { 2, 4 })
{
    var indices = new byte[9 * indexSize];
    for (int i = 0; i < 9; i++) { var value = indexSize == 2 ? BitConverter.GetBytes((ushort)i) : BitConverter.GetBytes(i); value.CopyTo(indices, i * indexSize); }
    var prior = (byte[])indices.Clone(); MirrorGeometry.ReverseTriangles(indices, 3, 3, indexSize);
    int Read(int n) => indexSize == 2 ? BitConverter.ToUInt16(indices, n * indexSize) : BitConverter.ToInt32(indices, n * indexSize);
    Check(Read(3) == 3 && Read(4) == 5 && Read(5) == 4, "Reverse only the selected submesh's winding.");
    Check(indices.Take(3 * indexSize).SequenceEqual(prior.Take(3 * indexSize)) && indices.Skip(6 * indexSize).SequenceEqual(prior.Skip(6 * indexSize)), "Preserve neighbor index ranges.");
    MirrorGeometry.ReverseTriangles(indices, 3, 3, indexSize); Check(indices.SequenceEqual(prior), "Winding reflection roundtrip.");
}
Reject(() => MirrorGeometry.ReverseTriangles(new byte[12], 0, 2, 4), "Reject partial triangle.");
Reject(() => MirrorGeometry.ReverseTriangles(new byte[12], 2, 3, 4), "Reject out-of-buffer triangle range.");
Reject(() => MirrorGeometry.ReverseTriangles(new byte[12], -1, 3, 4), "Reject negative submesh start.");
Reject(() => MirrorGeometry.ReverseTriangles(new byte[12], int.MaxValue, 3, 4), "Reject integer-overflow attack range.");
Console.WriteLine($"Mirror geometry: {checks} assertions passed.");
