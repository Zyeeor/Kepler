// 按天滚动的日志文件 Writer：每天一个文件（YYYY-MM-DD.log），跨天自动切换。
package logx

import (
	"os"
	"path/filepath"
	"sync"
	"time"
)

// DailyWriter 按天滚动的日志文件 writer：写入时检查日期，跨天自动切换到当天文件。
// 文件以追加模式打开，不轮转清理——单机 Demo 阶段人工管理即可。
type DailyWriter struct {
	dir  string
	mu   sync.Mutex
	day  string
	file *os.File
}

// NewDailyWriter 创建按天日志 writer（目录不存在时自动创建）。
func NewDailyWriter(dir string) (*DailyWriter, error) {
	if err := os.MkdirAll(dir, 0o755); err != nil {
		return nil, err
	}
	w := &DailyWriter{dir: dir}
	if err := w.rotateLocked(); err != nil {
		return nil, err
	}
	return w, nil
}

// rotateLocked 日期变化时切换到新文件（首次调用必然切换）。
func (w *DailyWriter) rotateLocked() error {
	day := time.Now().Format("2006-01-02")
	if day == w.day && w.file != nil {
		return nil
	}
	f, err := os.OpenFile(filepath.Join(w.dir, day+".log"), os.O_CREATE|os.O_WRONLY|os.O_APPEND, 0o644)
	if err != nil {
		return err
	}
	if w.file != nil {
		_ = w.file.Close()
	}
	w.file = f
	w.day = day
	return nil
}

// Write 实现 io.Writer。
func (w *DailyWriter) Write(p []byte) (int, error) {
	w.mu.Lock()
	defer w.mu.Unlock()
	if err := w.rotateLocked(); err != nil {
		return 0, err
	}
	return w.file.Write(p)
}

// Close 关闭当前日志文件。
func (w *DailyWriter) Close() error {
	w.mu.Lock()
	defer w.mu.Unlock()
	if w.file != nil {
		return w.file.Close()
	}
	return nil
}
