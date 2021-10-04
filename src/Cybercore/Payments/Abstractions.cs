using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Cybercore.Configuration;
using Cybercore.Mining;
using Cybercore.Persistence.Model;

namespace Cybercore.Payments
{
    public interface IPayoutHandler
    {
        Task ConfigureAsync(ClusterConfig clusterConfig, PoolConfig poolConfig, CancellationToken ct);
        Task<Block[]> ClassifyBlocksAsync(IMiningPool pool, Block[] blocks, CancellationToken ct);
        Task CalculateBlockEffortAsync(IMiningPool pool, Block block, double accumulatedBlockShareDiff, CancellationToken ct);
        Task<decimal> UpdateBlockRewardBalancesAsync(IDbConnection con, IDbTransaction tx, IMiningPool pool, Block block, CancellationToken ct);
        Task PayoutAsync(IMiningPool pool, Balance[] balances, CancellationToken ct);
        double AdjustShareDifficulty(double difficulty);

        string FormatAmount(decimal amount);
    }

    public interface IPayoutScheme
    {
        Task UpdateBalancesAsync(IDbConnection con, IDbTransaction tx, IMiningPool pool, IPayoutHandler payoutHandler, Block block, decimal blockReward, CancellationToken ct);
    }
}