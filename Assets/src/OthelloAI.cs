using UnityEngine;

// 盤面スナップショット上で最善手を探索する Unity 非依存の AI。
// 評価値はスコア差（aiColor 視点）を最大化するゼロサム・ミニマックス。
internal static class OthelloAI
{
    private const int Size = 8;
    private static readonly int[] Dx = { -1, -1, -1,  0, 0,  1, 1, 1 };
    private static readonly int[] Dy = { -1,  0,  1, -1, 1, -1, 0, 1 };

    // 最善手を返す。打てる手が無ければ (-1, -1)
    public static (int x, int y) GetBestMove(DiskColor[][] board, DiskColor aiColor, int totalDisks, int depth)
    {
        int bestScore = int.MinValue;
        (int x, int y) best = (-1, -1);

        for (int x = 0; x < Size; x++)
            for (int y = 0; y < Size; y++)
            {
                int flipped = CountFlips(board, x, y, aiColor);
                if (flipped == 0) continue;

                var next = Clone(board);
                ApplyMove(next, x, y, aiColor);
                int newTotal = totalDisks + 1;
                int points = Points(flipped, newTotal);

                int score = Minimax(next, Opponent(aiColor), aiColor, newTotal, depth - 1, points);

                if (score > bestScore)
                {
                    bestScore = score;
                    best = (x, y);
                }
            }

        return best;
    }

    // accDiff: ここまでの (aiColor 視点) スコア差。turn が aiColor なら max、相手なら min。
    private static int Minimax(DiskColor[][] board, DiskColor turn, DiskColor aiColor,
                               int totalDisks, int depth, int accDiff)
    {
        if (depth == 0 || !HasMoves(board, turn))
            return accDiff;

        bool maximizing = turn == aiColor;
        int best = maximizing ? int.MinValue : int.MaxValue;

        for (int x = 0; x < Size; x++)
            for (int y = 0; y < Size; y++)
            {
                int flipped = CountFlips(board, x, y, turn);
                if (flipped == 0) continue;

                var next = Clone(board);
                ApplyMove(next, x, y, turn);
                int newTotal = totalDisks + 1;
                int points = Points(flipped, newTotal);
                int diff = accDiff + (maximizing ? points : -points);

                int score = Minimax(next, Opponent(turn), aiColor, newTotal, depth - 1, diff);

                best = maximizing ? Mathf.Max(best, score) : Mathf.Min(best, score);
            }

        return best;
    }

    // 既存 AddScore と同じ式
    private static int Points(int flipped, int totalDisks)
        => Mathf.RoundToInt((float)flipped / totalDisks * Size * Size);

    // (x,y) に color を置いたとき返せる枚数（置けない/空でない場合は 0）
    private static int CountFlips(DiskColor[][] board, int x, int y, DiskColor color)
    {
        if (board[x][y] != DiskColor.None) return 0;

        DiskColor opponent = Opponent(color);
        int total = 0;

        for (int d = 0; d < 8; d++)
        {
            int nx = x + Dx[d];
            int ny = y + Dy[d];
            int count = 0;

            while (nx is >= 0 and < Size && ny is >= 0 and < Size && board[nx][ny] == opponent)
            {
                nx += Dx[d];
                ny += Dy[d];
                count++;
            }

            if (count > 0 && nx is >= 0 and < Size && ny is >= 0 and < Size && board[nx][ny] == color)
                total += count;
        }

        return total;
    }

    // 盤を書き換えて (x,y) に着手。返した枚数を返す。
    private static int ApplyMove(DiskColor[][] board, int x, int y, DiskColor color)
    {
        board[x][y] = color;
        DiskColor opponent = Opponent(color);
        int flipped = 0;

        for (int d = 0; d < 8; d++)
        {
            int nx = x + Dx[d];
            int ny = y + Dy[d];
            int count = 0;

            while (nx is >= 0 and < Size && ny is >= 0 and < Size && board[nx][ny] == opponent)
            {
                nx += Dx[d];
                ny += Dy[d];
                count++;
            }

            if (count > 0 && nx is >= 0 and < Size && ny is >= 0 and < Size && board[nx][ny] == color)
            {
                int bx = x + Dx[d];
                int by = y + Dy[d];
                for (int i = 0; i < count; i++)
                {
                    board[bx][by] = color;
                    bx += Dx[d];
                    by += Dy[d];
                }
                flipped += count;
            }
        }

        return flipped;
    }

    private static bool HasMoves(DiskColor[][] board, DiskColor color)
    {
        for (int x = 0; x < Size; x++)
            for (int y = 0; y < Size; y++)
                if (CountFlips(board, x, y, color) > 0) return true;
        return false;
    }

    private static DiskColor[][] Clone(DiskColor[][] board)
    {
        var copy = new DiskColor[Size][];
        for (int i = 0; i < Size; i++)
            copy[i] = (DiskColor[])board[i].Clone();
        return copy;
    }

    private static DiskColor Opponent(DiskColor c)
        => c == DiskColor.Black ? DiskColor.White : DiskColor.Black;
}
