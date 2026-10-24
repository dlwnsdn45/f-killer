using System;
using System.Collections.Generic;

public static class ArrayExtensions
{
    /// <summary>
    /// 배열의 모든 요소를 초기화합니다.
    /// </summary>
    /// <param name="source"></param>
    public static void Clear<T>(this T[] source)
    {
        Array.Clear(source, 0, source.Length);
    }

    /// <summary>
    /// 배열에 존재하는 값의 인덱스를 가져옵니다. 값이 없을 경우 -1을 반환합니다.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="source"></param>
    /// <param name="value"></param>
    /// <returns></returns>
    public static int IndexOf<T>(this T[] source, T value)
    {
        return Array.IndexOf(source, value);
    }

    public static int FindIndex<T>(this T[] source, Predicate<T> match)
    {
        return Array.FindIndex(source, match);
    }

    /// <summary>
    /// 배열에 값이 존재하는지 확인합니다.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="source"></param>
    /// <param name="value"></param>
    /// <returns></returns>
    public static bool Contains<T>(this T[] source, T value)
    {
        int index = source.IndexOf(value);
        return index >= 0 && index < source.Length;
    }

    public static bool Exists<T>(this T[] source, Predicate<T> match)
    {
        return Array.Exists(source, match);
    }

    public static void Copy<T>(this T[] source, T[] destination, int length)
    {
        Array.Copy(source, destination, length);
    }

    /// <summary>
    /// 배열 내 빈 자리(default)에 새 값을 넣습니다.
    /// </summary>
    /// <param name="source"></param>
    /// <param name="value"></param>
    /// <returns>성공 여부</returns>
    public static bool TryInsert<T>(this T[] source, T value)
    {
        EqualityComparer<T> comparer = EqualityComparer<T>.Default;
        for (int i = 0; i < source.Length; i++)
        {
            if (comparer.Equals(source[i], default))
            {
                source[i] = value;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 배열 내 빈 자리(default)에 새 값을 넣습니다.
    /// </summary>
    /// <param name="source"></param>
    /// <param name="value"></param>
    /// <returns></returns>
    public static void Insert<T>(this T[] source, T value)
    {
        EqualityComparer<T> comparer = EqualityComparer<T>.Default;
        for (int i = 0; i < source.Length; i++)
        {
            if (comparer.Equals(source[i], default))
            {
                source[i] = value;
                break;
            }
        }
    }

    /// <summary>
    /// 배열 내 빈자리를 찾아 다른 배열의 모든 값을 넣습니다.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="source"></param>
    /// <param name="array"></param>
    public static void InsertArray<T>(this T[] source, T[] array)
    {
        int index = 0;
        EqualityComparer<T> comparer = EqualityComparer<T>.Default;

        for (int i = 0; i < source.Length && index < array.Length; i++)
        {
            if (comparer.Equals(source[i], default))
            {
                source[i] = source[index];
                index++;
            }
        }
    }

    /// <summary>
    /// 배열의 마지막 값을 가져옵니다.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="source"></param>
    /// <returns></returns>
    public static T Last<T>(this T[] source)
    {
        return source[^1];
    }

    /// <summary>
    /// 배열의 값 중 랜덤한 값을 가져옵니다.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="source"></param>
    /// <param name="random"></param>
    /// <returns></returns>
    public static T Random<T>(this T[] source, System.Random random = null)
    {
        if (source.Length == 0)
        {
            throw new Exception("source length is zero.");
        }

        return source[(random != null) ? random.Next(0, source.Length) : UnityEngine.Random.Range(0, source.Length)];
    }

    /// <summary>
    /// 배열의 모든 요소를 바꿔줍니다.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="source"></param>
    /// <param name="value"></param>
    public static void SetAll<T>(this T[] source, T value)
    {
        for (int i = 0; i < source.Length; i++)
        {
            source[i] = value;
        }
    }

    /// <summary>
    /// 값이 default인 경우를 제외한 배열의 길이를 가져옵니다.
    /// </summary>
    /// <param name="source"></param>
    /// <returns></returns>
    public static int ValidLength<T>(this T[] source)
    {
        int count = 0;
        EqualityComparer<T> comparer = EqualityComparer<T>.Default;
        for (int i = 0; i < source.Length; i++)
        {
            if (!comparer.Equals(source[i], default))
            {
                count++;
            }
        }

        return count;
    }


}