// UGC 域存储抽象与数据模型。
package ugc

import "errors"

// ErrNotFound 目标记录不存在。
var ErrNotFound = errors.New("not found")

// Store UGC 存储接口（抽象，便于切换 MySQL）。
type Store interface {
	// UGC 内容
	CreateCreation(c *Creation) error
	GetCreation(id string) (*Creation, error)
	ListCreations(filter *CreationFilter) ([]*Creation, int, error)
	SearchCreations(keyword string, creationType string, page, pageSize int) ([]*Creation, int, error)
	IncrementDownloads(id string) error

	// 订阅
	Subscribe(playerID, creationID string) error
	Unsubscribe(playerID, creationID string) error
	IsSubscribed(playerID, creationID string) (bool, error)

	// 评分
	RateCreation(playerID, creationID string, rating int, comment string) error
}

// Creation UGC 创作内容。
type Creation struct {
	ID           string
	CreatorID    string
	CreatorName  string
	Type         string // map | monster | template
	Name         string
	Description  string
	Tags         []string
	FileURL      string
	ThumbnailURL string
	Status       string // draft | published | reviewing | banned
	Downloads    int
	Likes        int
	Rating       float64
	Version      int
	CreatedAt    int64
	UpdatedAt    int64
}

// CreationFilter 查询过滤器。
type CreationFilter struct {
	Type       string
	Page       int
	PageSize   int
	SortBy     string // downloads | rating | created_at
	Descending bool
}
