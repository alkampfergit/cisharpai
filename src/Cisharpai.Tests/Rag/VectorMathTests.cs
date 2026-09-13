using Cisharpai.Rag;

namespace Cisharpai.Tests.Rag;

[TestFixture]
public class VectorMathTests
{
    #region DotProduct

    [Test]
    public void DotProduct_ComputesCorrectly()
    {
        float[] a = [1f, 2f, 3f];
        float[] b = [4f, 5f, 6f];
        Assert.That(VectorMath.DotProduct(a, b), Is.EqualTo(32f));
    }

    [Test]
    public void DotProduct_Span_ComputesCorrectly()
    {
        ReadOnlySpan<float> a = [1f, 0f, 0f];
        ReadOnlySpan<float> b = [0f, 1f, 0f];
        Assert.That(VectorMath.DotProduct(a, b), Is.EqualTo(0f));
    }

    [Test]
    public void DotProduct_EmptyVectors_ReturnsZero()
    {
        Assert.That(VectorMath.DotProduct(Array.Empty<float>(), Array.Empty<float>()), Is.EqualTo(0f));
    }

    [Test]
    public void DotProduct_MismatchedDimensions_Throws()
    {
        Assert.That(
            () => VectorMath.DotProduct(new float[] { 1f }, new float[] { 1f, 2f }),
            Throws.TypeOf<ArgumentException>());
    }

    [Test]
    public void DotProduct_NullArray_Throws()
    {
        Assert.That(() => VectorMath.DotProduct((float[])null!, new float[] { 1f }), Throws.ArgumentNullException);
        Assert.That(() => VectorMath.DotProduct(new float[] { 1f }, (float[])null!), Throws.ArgumentNullException);
    }

    [Test]
    public void DotProduct_IReadOnlyList_ComputesCorrectly()
    {
        IReadOnlyList<float> a = new float[] { 2f, 3f };
        IReadOnlyList<float> b = new float[] { 4f, 5f };
        Assert.That(VectorMath.DotProduct(a, b), Is.EqualTo(23f));
    }

    [Test]
    public void DotProduct_IReadOnlyList_NonArrayList_ComputesCorrectly()
    {
        IReadOnlyList<float> a = new List<float> { 2f, 3f };
        IReadOnlyList<float> b = new List<float> { 4f, 5f };
        Assert.That(VectorMath.DotProduct(a, b), Is.EqualTo(23f));
    }

    #endregion

    #region CosineSimilarity

    [Test]
    public void CosineSimilarity_IdenticalVectors_ReturnsOne()
    {
        float[] v = [3f, 4f];
        Assert.That(VectorMath.CosineSimilarity(v, v), Is.EqualTo(1f).Within(1e-6f));
    }

    [Test]
    public void CosineSimilarity_OrthogonalVectors_ReturnsZero()
    {
        float[] a = [1f, 0f];
        float[] b = [0f, 1f];
        Assert.That(VectorMath.CosineSimilarity(a, b), Is.EqualTo(0f).Within(1e-6f));
    }

    [Test]
    public void CosineSimilarity_OppositeVectors_ReturnsNegativeOne()
    {
        float[] a = [1f, 0f];
        float[] b = [-1f, 0f];
        Assert.That(VectorMath.CosineSimilarity(a, b), Is.EqualTo(-1f).Within(1e-6f));
    }

    [Test]
    public void CosineSimilarity_ZeroVector_ReturnsZero()
    {
        float[] zero = [0f, 0f, 0f];
        float[] v = [1f, 2f, 3f];
        Assert.That(VectorMath.CosineSimilarity(zero, v), Is.EqualTo(0f));
        Assert.That(VectorMath.CosineSimilarity(v, zero), Is.EqualTo(0f));
    }

    [Test]
    public void CosineSimilarity_BothZeroVectors_ReturnsZero()
    {
        float[] zero = [0f, 0f];
        Assert.That(VectorMath.CosineSimilarity(zero, zero), Is.EqualTo(0f));
    }

    [Test]
    public void CosineSimilarity_EmptyVectors_ReturnsZero()
    {
        Assert.That(VectorMath.CosineSimilarity(Array.Empty<float>(), Array.Empty<float>()), Is.EqualTo(0f));
    }

    [Test]
    public void CosineSimilarity_MismatchedDimensions_Throws()
    {
        Assert.That(
            () => VectorMath.CosineSimilarity(new float[] { 1f, 2f }, new float[] { 1f }),
            Throws.TypeOf<ArgumentException>());
    }

    [Test]
    public void CosineSimilarity_NullArray_Throws()
    {
        Assert.That(() => VectorMath.CosineSimilarity((float[])null!, new float[] { 1f }), Throws.ArgumentNullException);
    }

    [Test]
    public void CosineSimilarity_IReadOnlyList_ComputesCorrectly()
    {
        IReadOnlyList<float> a = new float[] { 1f, 0f };
        IReadOnlyList<float> b = new float[] { 1f, 0f };
        Assert.That(VectorMath.CosineSimilarity(a, b), Is.EqualTo(1f).Within(1e-6f));
    }

    [Test]
    public void CosineSimilarity_IReadOnlyList_NonArrayList_ComputesCorrectly()
    {
        IReadOnlyList<float> a = new List<float> { 3f, 4f };
        IReadOnlyList<float> b = new List<float> { 3f, 4f };
        Assert.That(VectorMath.CosineSimilarity(a, b), Is.EqualTo(1f).Within(1e-6f));
    }

    [Test]
    public void CosineSimilarity_ScaledVectors_ReturnsOne()
    {
        float[] a = [1f, 2f, 3f];
        float[] b = [2f, 4f, 6f];
        Assert.That(VectorMath.CosineSimilarity(a, b), Is.EqualTo(1f).Within(1e-6f));
    }

    [Test]
    public void CosineSimilarity_LargeMagnitudeVectors_DoesNotOverflowToNaN()
    {
        float[] a = [1e20f];
        float[] b = [1e20f];
        var result = VectorMath.CosineSimilarity(a, b);
        Assert.That(float.IsNaN(result), Is.False);
        Assert.That(result, Is.EqualTo(1f).Within(1e-6f));
    }

    [Test]
    public void CosineSimilarity_TinyMagnitudeVectors_DoesNotReturnNaN()
    {
        float[] a = [float.Epsilon];
        float[] b = [float.Epsilon];
        var result = VectorMath.CosineSimilarity(a, b);
        Assert.That(float.IsNaN(result), Is.False);
        Assert.That(result, Is.EqualTo(1f).Within(1e-6f));
    }

    #endregion

    #region Normalize

    [Test]
    public void Normalize_ReturnsUnitVector()
    {
        float[] v = [3f, 4f];
        var result = VectorMath.Normalize((ReadOnlySpan<float>)v);

        Assert.Multiple(() =>
        {
            Assert.That(result, Has.Length.EqualTo(2));
            Assert.That(result[0], Is.EqualTo(0.6f).Within(1e-6f));
            Assert.That(result[1], Is.EqualTo(0.8f).Within(1e-6f));
        });
    }

    [Test]
    public void Normalize_DoesNotMutateInput()
    {
        float[] v = [3f, 4f];
        _ = VectorMath.Normalize((ReadOnlySpan<float>)v);
        Assert.That(v, Is.EqualTo(new float[] { 3f, 4f }));
    }

    [Test]
    public void Normalize_ZeroVector_ReturnsZeroVector()
    {
        float[] zero = [0f, 0f, 0f];
        var result = VectorMath.Normalize((ReadOnlySpan<float>)zero);
        Assert.That(result, Is.EqualTo(new float[] { 0f, 0f, 0f }));
    }

    [Test]
    public void Normalize_ZeroVector_NoNaN()
    {
        float[] zero = [0f, 0f];
        var result = VectorMath.Normalize((ReadOnlySpan<float>)zero);
        Assert.That(result.Any(float.IsNaN), Is.False);
    }

    [Test]
    public void Normalize_EmptyVector_ReturnsEmpty()
    {
        var result = VectorMath.Normalize(ReadOnlySpan<float>.Empty);
        Assert.That(result, Is.Empty);
    }

    [Test]
    public void Normalize_AlreadyUnit_RemainsUnit()
    {
        float[] unit = [1f, 0f, 0f];
        var result = VectorMath.Normalize((ReadOnlySpan<float>)unit);
        Assert.That(result, Is.EqualTo(new float[] { 1f, 0f, 0f }));
    }

    [Test]
    public void Normalize_IReadOnlyList_ReturnsUnitVector()
    {
        IReadOnlyList<float> v = new float[] { 0f, 5f };
        var result = VectorMath.Normalize(v);
        Assert.That(result[1], Is.EqualTo(1f).Within(1e-6f));
    }

    [Test]
    public void Normalize_IReadOnlyList_NonArrayList_ReturnsUnitVector()
    {
        IReadOnlyList<float> v = new List<float> { 3f, 4f };
        var result = VectorMath.Normalize(v);
        Assert.Multiple(() =>
        {
            Assert.That(result[0], Is.EqualTo(0.6f).Within(1e-6f));
            Assert.That(result[1], Is.EqualTo(0.8f).Within(1e-6f));
        });
    }

    #endregion

    #region NormalizeInPlace

    [Test]
    public void NormalizeInPlace_MutatesInput()
    {
        float[] v = [3f, 4f];
        VectorMath.NormalizeInPlace(v);
        Assert.Multiple(() =>
        {
            Assert.That(v[0], Is.EqualTo(0.6f).Within(1e-6f));
            Assert.That(v[1], Is.EqualTo(0.8f).Within(1e-6f));
        });
    }

    [Test]
    public void NormalizeInPlace_ZeroVector_LeavesUnchanged()
    {
        float[] zero = [0f, 0f];
        VectorMath.NormalizeInPlace(zero);
        Assert.That(zero, Is.EqualTo(new float[] { 0f, 0f }));
    }

    [Test]
    public void NormalizeInPlace_ZeroVector_NoNaN()
    {
        float[] zero = [0f, 0f, 0f];
        VectorMath.NormalizeInPlace(zero);
        Assert.That(zero.Any(float.IsNaN), Is.False);
    }

    [Test]
    public void NormalizeInPlace_ProducesUnitMagnitude()
    {
        float[] v = [1f, 2f, 3f, 4f];
        VectorMath.NormalizeInPlace(v);
        var magnitude = MathF.Sqrt(v.Sum(x => x * x));
        Assert.That(magnitude, Is.EqualTo(1f).Within(1e-6f));
    }

    [Test]
    public void NormalizeInPlace_Empty_DoesNotThrow()
    {
        Assert.DoesNotThrow(() => VectorMath.NormalizeInPlace(Span<float>.Empty));
    }

    [Test]
    public void NormalizeInPlace_LargeMagnitudeVector_ProducesUnitLength()
    {
        float[] v = [1e20f, 1e20f];
        VectorMath.NormalizeInPlace(v);
        Assert.Multiple(() =>
        {
            Assert.That(v.Any(float.IsInfinity), Is.False);
            Assert.That(v.Any(float.IsNaN), Is.False);
            var magnitude = MathF.Sqrt(v.Sum(x => x * x));
            Assert.That(magnitude, Is.EqualTo(1f).Within(1e-6f));
        });
    }

    [Test]
    public void NormalizeInPlace_FloatEpsilonVector_ProducesUnitLength()
    {
        float[] v = [float.Epsilon];
        VectorMath.NormalizeInPlace(v);
        Assert.Multiple(() =>
        {
            Assert.That(float.IsInfinity(v[0]), Is.False);
            Assert.That(float.IsNaN(v[0]), Is.False);
            Assert.That(v[0], Is.EqualTo(1f).Within(1e-6f));
        });
    }

    #endregion

    #region TopK

    [Test]
    public void TopK_ReturnsCandidatesInDescendingScoreOrder()
    {
        float[] query = [1f, 0f];
        float[][] candidates =
        [
            [0f, 1f],  // orthogonal → 0
            [1f, 0f],  // identical → 1
            [-1f, 0f], // opposite → -1
        ];

        var results = VectorMath.TopK(query, candidates, 2);

        Assert.Multiple(() =>
        {
            Assert.That(results, Has.Length.EqualTo(2));
            Assert.That(results[0].Index, Is.EqualTo(1));
            Assert.That(results[0].Score, Is.EqualTo(1f).Within(1e-6f));
            Assert.That(results[1].Index, Is.EqualTo(0));
            Assert.That(results[1].Score, Is.EqualTo(0f).Within(1e-6f));
        });
    }

    [Test]
    public void TopK_KExceedsCandidates_ReturnsAll()
    {
        float[] query = [1f, 0f];
        float[][] candidates = [[1f, 0f], [0f, 1f]];

        var results = VectorMath.TopK(query, candidates, 10);
        Assert.That(results, Has.Length.EqualTo(2));
    }

    [Test]
    public void TopK_EmptyCandidates_ReturnsEmpty()
    {
        var results = VectorMath.TopK(new float[] { 1f }, Array.Empty<float[]>(), 5);
        Assert.That(results, Is.Empty);
    }

    [Test]
    public void TopK_KZeroOrNegative_Throws()
    {
        Assert.That(
            () => VectorMath.TopK(new float[] { 1f }, new float[][] { [1f] }, 0),
            Throws.TypeOf<ArgumentOutOfRangeException>());
        Assert.That(
            () => VectorMath.TopK(new float[] { 1f }, new float[][] { [1f] }, -1),
            Throws.TypeOf<ArgumentOutOfRangeException>());
    }

    [Test]
    public void TopK_MismatchedDimensions_Throws()
    {
        float[] query = [1f, 2f];
        float[][] candidates = [[1f]];

        Assert.That(
            () => VectorMath.TopK(query, candidates, 1),
            Throws.TypeOf<ArgumentException>());
    }

    [Test]
    public void TopK_NullArray_Throws()
    {
        Assert.That(
            () => VectorMath.TopK((IReadOnlyList<float>)null!, new float[][] { [1f] }, 1),
            Throws.ArgumentNullException);
        Assert.That(
            () => VectorMath.TopK(new float[] { 1f }, (IReadOnlyList<float[]>)null!, 1),
            Throws.ArgumentNullException);
    }

    [Test]
    public void TopK_IReadOnlyList_ComputesCorrectly()
    {
        IReadOnlyList<float> query = new float[] { 1f, 0f };
        IReadOnlyList<float[]> candidates = new float[][] { [0f, 1f], [1f, 0f] };

        var results = VectorMath.TopK(query, candidates, 1);
        Assert.That(results[0].Index, Is.EqualTo(1));
    }

    [Test]
    public void TopK_IReadOnlyList_NonArrayList_ComputesCorrectly()
    {
        IReadOnlyList<float> query = new List<float> { 1f, 0f };
        IReadOnlyList<float[]> candidates = new List<float[]>
        {
            new[] { 0f, 1f },
            new[] { 1f, 0f },
        };

        var results = VectorMath.TopK(query, candidates, 1);
        Assert.That(results[0].Index, Is.EqualTo(1));
    }

    [Test]
    public void TopK_NullCandidate_ThrowsWithIndex()
    {
        float[] query = [1f, 0f];
        float[][] candidates = [new float[] { 1f, 0f }, null!, new float[] { 0f, 1f }];

        var ex = Assert.Throws<ArgumentNullException>(
            () => VectorMath.TopK(query, candidates, 1));
        Assert.That(ex!.Message, Does.Contain("index 1"));
    }

    [Test]
    public void TopK_NullCandidate_IReadOnlyList_ThrowsWithIndex()
    {
        IReadOnlyList<float> query = new float[] { 1f, 0f };
        IReadOnlyList<float[]> candidates = new List<float[]>
        {
            new[] { 1f, 0f },
            null!,
            new[] { 0f, 1f },
        };

        var ex = Assert.Throws<ArgumentNullException>(
            () => VectorMath.TopK(query, candidates, 1));
        Assert.That(ex!.Message, Does.Contain("index 1"));
    }

    [Test]
    public void TopK_PreservesOriginalIndices()
    {
        float[] query = [1f, 1f];
        float[][] candidates =
        [
            [0f, 1f],
            [1f, 1f],
            [1f, 0f],
            [-1f, -1f],
        ];

        var results = VectorMath.TopK(query, candidates, 4);

        Assert.Multiple(() =>
        {
            Assert.That(results[0].Index, Is.EqualTo(1));
            Assert.That(results[^1].Index, Is.EqualTo(3));
            Assert.That(results[^1].Score, Is.EqualTo(-1f).Within(1e-6f));
        });
    }

    #endregion
}
